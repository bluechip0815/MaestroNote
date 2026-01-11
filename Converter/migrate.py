import mysql.connector
from mysql.connector import Error
import argparse
import sys
import os
import unicodedata
import re
import json

# ============================================================
# KONFIGURATION DER VERBINDUNGSDATEN (aus Umgebungsvariablen)
# ============================================================
DB_CONFIG = {
    "old": {
        "host": os.getenv("OLD_DB_HOST", "localhost"),
        "user": os.getenv("OLD_DB_USER"),
        "password": os.getenv("OLD_DB_PASSWORD"),
        "database": os.getenv("OLD_DB_NAME"),
        "port": int(os.getenv("OLD_DB_PORT", "3306"))
    },
    "new": {
        "host": os.getenv("NEW_DB_HOST", "localhost"),
        "user": os.getenv("NEW_DB_USER"),
        "password": os.getenv("NEW_DB_PASSWORD"),
        "database": os.getenv("NEW_DB_NAME"),
        "port": int(os.getenv("NEW_DB_PORT", "3306"))
    }
}

class MigrationSpecialist:
    def __init__(self, only_old_db=False):
        try:
            self.old_conn = mysql.connector.connect(**DB_CONFIG["old"])
            self.old_cur = self.old_conn.cursor(dictionary=True)
            print("✓ Alte Datenbank verbunden.")

            if not only_old_db:
                self.new_conn = mysql.connector.connect(**DB_CONFIG["new"])
                self.new_cur = self.new_conn.cursor()
                print("✓ Neue Datenbank verbunden.")
            else:
                self.new_conn = None
                self.new_cur = None

        except Error as e:
            print(f"❌ Verbindungsfehler: {e}")
            sys.exit(1)

        # Caching um Duplikate in den Stammdaten zu vermeiden
        # Key: Normalized String, Value: ID in DB
        self.cache = {'dirigent': {}, 'orchester': {}, 'komponist': {}, 'werk': {}, 'solist': {}, 'ort': {}}
        self.correction_map = {}
        self.todo_counter = 1

    def normalize_name(self, name):
        """
        Normalisiert Namen für den Vergleich:
        - Kleinbuchstaben
        - Entfernt Akzente
        - Standardisiert 'v.' -> 'van'
        - Trimmt Whitespace
        """
        if not name:
            return ""

        # 1. Lowercase
        s = name.lower()

        # 2. Remove accents
        s = ''.join(c for c in unicodedata.normalize('NFD', s) if unicodedata.category(c) != 'Mn')

        # 3. Replace 'v.' or 'v ' with 'van '
        # Matches 'v.' or 'v' followed by space at word boundary
        s = re.sub(r'\bv\.?\s+', 'van ', s)

        # 4. Trim
        return s.strip()

    def split_name(self, full_name):
        if not full_name: return "", ""
        parts = full_name.strip().split(' ')
        if len(parts) == 1: return "", parts[0]
        return " ".join(parts[:-1]), parts[-1]

    def split_komponist_string(self, val):
        """
        Splits Komponist string by '/', ',', and ' und '.
        Returns a list of clean names.
        """
        if not val:
            return []

        # Replace ' und ' with special token or just unify splitters
        # Use regex to split by:
        # 1. '/'
        # 2. ','
        # 3. ' und ' (case insensitive)

        # Pattern: \s*[/,]\s* | \s+und\s+ (case insensitive handled by re.IGNORECASE)
        pattern = r"\s*[/,]\s*|\s+und\s+"
        parts = re.split(pattern, val, flags=re.IGNORECASE)

        # Clean up empty strings
        return [p.strip() for p in parts if p.strip()]

    def load_correction_map(self, filepath):
        if not filepath: return
        if not os.path.exists(filepath):
            print(f"⚠️ Korrektur-Map nicht gefunden: {filepath}")
            return

        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                self.correction_map = json.load(f)
            print(f"✓ Korrektur-Map geladen ({len(self.correction_map)} Einträge).")
        except Exception as e:
            print(f"❌ Fehler beim Laden der Korrektur-Map: {e}")

    def generate_correction_map(self, output_path):
        print("🔍 Analysiere Daten auf Duplikate...")

        # Dictionary: category -> { normalized_key -> first_seen_original }
        seen_normalized = {
            'dirigent': {},
            'orchester': {},
            'komponist': {},
            'solist': {},
            'ort': {}
        }

        duplicates_map = {} # { "Duplicate Spelling": "First Spelling" }

        self.old_cur.execute("SELECT * FROM MusicRecords")
        old_data = self.old_cur.fetchall()

        count_duplicates = 0

        for row in old_data:
            # Helper to process a value
            def process_value(category, val):
                if not val: return
                norm = self.normalize_name(val)
                if not norm: return

                if norm in seen_normalized[category]:
                    first_val = seen_normalized[category][norm]
                    if first_val != val:
                        # Found a duplicate!
                        # Add to map: val -> first_val
                        # But only if not already mapped or mapped differently
                        if val not in duplicates_map:
                            duplicates_map[val] = first_val
                            nonlocal count_duplicates
                            count_duplicates += 1
                else:
                    seen_normalized[category][norm] = val

            process_value('dirigent', row['Dirigent'])
            process_value('orchester', row['Orchester'])
            process_value('ort', row['Ort'])

            # Komponist split handling
            komponisten = self.split_komponist_string(row['Komponist'])
            for komp in komponisten:
                process_value('komponist', komp)

            # Solist special handling (comma separated)
            if row['Solist']:
                solist_clean = row['Solist'].replace("u.a.", "").replace("u. a.", "").strip()
                if solist_clean:
                    for s_full in [s.strip() for s in solist_clean.split(',')]:
                        process_value('solist', s_full)

        print(f"✓ Analyse beendet. {count_duplicates} Duplikate gefunden.")

        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(duplicates_map, f, indent=4, ensure_ascii=False)
            print(f"💾 Map gespeichert unter: {output_path}")
        except Exception as e:
            print(f"❌ Fehler beim Speichern der Map: {e}")

    def clear_new_db(self):
        print("⚠️ Lösche neue Datenbank für frischen Import...")
        # EF Core Standard-Tabellennamen für n:m Beziehungen
        tables = [
            "MusicRecordSolist", "MusicRecordWerk", "MusicRecords", 
            "Solisten", "Werke", "Komponisten", "Orchester", "Dirigenten", "Orte", "Documents"
        ]
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 0;")
        for table in tables:
            try: self.new_cur.execute(f"TRUNCATE TABLE {table};")
            except: pass
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 1;")
        self.new_conn.commit()

    def get_or_create(self, cache_key, lookup_val, insert_sql, params):
        if not lookup_val: return None

        # 1. Apply Correction Map
        val_to_use = self.correction_map.get(lookup_val, lookup_val)

        # 2. Normalize
        norm_key = self.normalize_name(val_to_use)

        # 3. Check Cache (using normalized key)
        if norm_key in self.cache[cache_key]:
            return self.cache[cache_key][norm_key]

        # 4. Insert (using the potentially corrected original value 'val_to_use')
        # Check if params need update based on override
        final_params = params
        if val_to_use != lookup_val:
            # We need to regenerate params based on val_to_use
            if cache_key in ['dirigent', 'solist', 'komponist']:
                # These use split_name
                parts = self.split_name(val_to_use)
                if cache_key == 'komponist':
                    final_params = (*parts, "")
                else:
                    final_params = parts
            elif cache_key in ['orchester', 'ort']:
                # These are just (Name,)
                final_params = (val_to_use,)
            elif cache_key == 'werk':
               pass

        self.new_cur.execute(insert_sql, final_params)
        new_id = self.new_cur.lastrowid
        self.cache[cache_key][norm_key] = new_id
        return new_id

    def transfer(self):
        print("🚀 Migration läuft...")
        self.old_cur.execute("SELECT * FROM MusicRecords")
        old_data = self.old_cur.fetchall()

        for row in old_data:
            # 1. Stammdaten (Dirigent & Orchester)
            d_id = self.get_or_create('dirigent', row['Dirigent'], 
                "INSERT INTO Dirigenten (Vorname, Name) VALUES (%s, %s)", self.split_name(row['Dirigent'])) if row['Dirigent'] else None
            
            o_id = self.get_or_create('orchester', row['Orchester'], 
                "INSERT INTO Orchester (Name) VALUES (%s)", (row['Orchester'],)) if row['Orchester'] else None

            ort_id = self.get_or_create('ort', row['Ort'],
                "INSERT INTO Orte (Name) VALUES (%s)", (row['Ort'],)) if row['Ort'] else None

            # 2. Komponist & Werk
            w_ids = [] # List of Work IDs to link

            if row['Werk']:
                komp_strings = self.split_komponist_string(row['Komponist'])
                
                # Check if we have multiple composers or just one (or none if Komponist field was empty but Werk wasn't?)
                # If Komponist empty, komp_strings is empty.
                if not komp_strings:
                     # Fallback: Work with no composer? Or skipped?
                     # Original logic: if row['Komponist'] else None
                     # If row['Komponist'] was empty, k_id was None.
                     # But Werk table expects KomponistId?
                     # Original SQL: INSERT INTO Werke ... VALUES (..., k_id) -> k_id can be None?
                     # Check schema... usually nullable or fails.
                     # Let's assume standard behavior: 1 Werk, No Composer.
                     pass

                if len(komp_strings) <= 1:
                    # Standard Case
                    k_str = komp_strings[0] if komp_strings else None
                    k_id = None
                    if k_str:
                        k_id = self.get_or_create('komponist', k_str,
                            "INSERT INTO Komponisten (Vorname, Name, Note) VALUES (%s, %s, %s)",
                            (*self.split_name(k_str), ""))

                    werk_key = f"{row['Werk']}_{k_id}"
                    w_id = self.get_or_create('werk', werk_key,
                        "INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)", (row['Werk'], k_id))
                    w_ids.append(w_id)
                else:
                    # Multi-Composer Case -> Create "Todo X" Works for each
                    for k_str in komp_strings:
                        k_id = self.get_or_create('komponist', k_str,
                            "INSERT INTO Komponisten (Vorname, Name, Note) VALUES (%s, %s, %s)",
                            (*self.split_name(k_str), ""))

                        # Create unique Todo work
                        todo_name = f"Todo {self.todo_counter}"
                        self.todo_counter += 1

                        # We don't really deduplicate Todo works (they are unique by counter), but we use get_or_create for consistency or direct insert?
                        # Using direct insert is safer to ensure uniqueness and not polluting cache with "Todo 1" etc if not needed?
                        # But get_or_create handles the cache logic.
                        # Cache key: "Todo 1_123".

                        werk_key = f"{todo_name}_{k_id}"
                        w_id = self.get_or_create('werk', werk_key,
                             "INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)", (todo_name, k_id))
                        w_ids.append(w_id)


            # 3. MusicRecord Hauptdatensatz (OHNE die alten Spalten 'Werk'/'Komponist')
            bewertung = f"{row['Bewertung1']}\n{row['Bewertung2']}".strip()
            sql_mr = """INSERT INTO MusicRecords 
                        (Id, Bezeichnung, Datum, Spielsaison, Bewertung, OrtId, DirigentId, OrchesterId)
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
            
            # Als 'Bezeichnung' nehmen wir den Namen des Werks (Original String)
            self.new_cur.execute(sql_mr, (
                row['Id'], row['Werk'], row['Datum'], row['Spielsaison'], 
                bewertung, ort_id, d_id, o_id
            ))

            # 4. n:m Beziehung: MusicRecord <-> Werk
            for w_id in w_ids:
                if w_id:
                    self.new_cur.execute(
                        "INSERT INTO MusicRecordWerk (MusicRecordsId, WerkeId) VALUES (%s, %s)",
                        (row['Id'], w_id)
                    )

            # 5. n:m Beziehung: MusicRecord <-> Solisten
            if row['Solist']:
                # Clean string before splitting: remove 'u.a.'/'u. a.', then trim
                solist_clean = row['Solist'].replace("u.a.", "").replace("u. a.", "").strip()
                if solist_clean:
                    for s_full in [s.strip() for s in solist_clean.split(',')]:
                        if not s_full: continue
                        s_id = self.get_or_create('solist', s_full,
                            "INSERT INTO Solisten (Vorname, Name) VALUES (%s, %s)", self.split_name(s_full))

                        self.new_cur.execute(
                            "INSERT INTO MusicRecordSolist (MusicRecordsId, SolistenId) VALUES (%s, %s)",
                            (row['Id'], s_id)
                        )

        # 6. Documents
        print("📂 Migriere Dokumente...")
        self.old_cur.execute("SELECT * FROM Documents")
        old_docs = self.old_cur.fetchall()

        for doc in old_docs:
            self.new_cur.execute(
                "INSERT INTO Documents (Id, FileName, EncryptedName, DocumentType, MusicRecordId) VALUES (%s, %s, %s, %s, %s)",
                (doc['Id'], doc['FileName'], doc['EncryptedName'], doc['DocumentType'], doc['MusicRecordId'])
            )

        self.new_conn.commit()
        print(f"✅ Fertig! {len(old_data)} Records und {len(old_docs)} Dokumente migriert.")

    def close(self):
        self.old_cur.close()
        self.old_conn.close()
        if self.new_cur: self.new_cur.close()
        if self.new_conn: self.new_conn.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--delete', action='store_true', help="Löscht existierende Daten in der Ziel-DB")
    parser.add_argument('--transfer', action='store_true', help="Führt die Migration durch")
    parser.add_argument('--generate-map', action='store_true', help="Erstellt eine JSON-Datei mit gefundenen Duplikaten (Korrektur-Map)")
    parser.add_argument('--map', type=str, help="Pfad zur Korrektur-Map JSON-Datei")
    args = parser.parse_args()
    
    # Init connection
    # If only generating map, we don't strictly need New DB connection, but let's keep it simple or optimize
    only_old = args.generate_map and not args.transfer and not args.delete
    spec = MigrationSpecialist(only_old_db=only_old)

    if args.generate_map:
        output_file = "correction-map.json"
        # If user provided a path via --map? Or just default?
        # User said: "an options --generate-map where all doubles are exported to (correction-map.json)"
        # Use default name or if map arg is present use that?
        # Let's assume default unless map arg is meant for input.
        # Usually --map is input. Let's output to "correction-map.json" by default.
        spec.generate_correction_map("correction-map.json")

    if args.map and args.transfer:
        spec.load_correction_map(args.map)

    if args.delete: spec.clear_new_db()
    if args.transfer: spec.transfer()

    spec.close()
