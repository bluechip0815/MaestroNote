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
            process_value('komponist', row['Komponist'])
            process_value('ort', row['Ort'])

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
        # Note: 'params' passed in usually contains 'lookup_val' or parts of it.
        # We need to re-derive params from 'val_to_use' if we modified it.
        # This is tricky because params structure varies.
        # Strategy: The caller passes 'params' derived from 'lookup_val'.
        # If 'val_to_use' != 'lookup_val', we might need to recalc params.

        # HOWEVER, the simplest way is to handle the param generation inside here or trust the caller?
        # The caller calls this like: get_or_create(..., row['Dirigent'], "INSERT...", split_name(row['Dirigent']))
        # If we change row['Dirigent'] to something else, the params are wrong.

        # Solution: We can't easily patch 'params'.
        # But we can assume that if we are doing normalization-based deduplication,
        # we only insert the FIRST one encountered.
        # The 'val_to_use' logic mainly helps if we have a manual override.

        # If we have a manual override (val_to_use != lookup_val), we MUST generate new params.
        # This requires knowing how to generate params for each type.

        # Let's refactor `get_or_create` to take a param-generator function?
        # Or just handle specific types inside here?
        # Or let the caller handle the lookup logic?
        # Caller handling is cleaner but repeats code.

        # Let's do a pragmatic check:
        final_params = params
        if val_to_use != lookup_val:
            # We need to regenerate params based on val_to_use
            if cache_key in ['dirigent', 'solist', 'komponist']:
                # These use split_name
                # Komponist also has an extra Note field (empty string)
                parts = self.split_name(val_to_use)
                if cache_key == 'komponist':
                    final_params = (*parts, "")
                else:
                    final_params = parts
            elif cache_key in ['orchester', 'ort']:
                # These are just (Name,)
                final_params = (val_to_use,)
            elif cache_key == 'werk':
                # Werk params are (Name, KomponistId).
                # Werk logic is handled separately in transfer usually because of KomponistId dependency.
                # But here, 'lookup_val' for Werk is 'Name_KomponistId'.
                # Normalization of Werk name is tricky.
                # If we map 'WerkName' -> 'WerkNameCorrected', we just need the name part.
                # But Werk key passed here is composite.
                # Let's assume for Werk we rely on the caching primarily.
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
            w_id = None
            if row['Werk']:
                k_id = self.get_or_create('komponist', row['Komponist'], 
                    "INSERT INTO Komponisten (Vorname, Name, Note) VALUES (%s, %s, %s)", 
                    (*self.split_name(row['Komponist']), "")) if row['Komponist'] else None
                
                # Werk deduplication is complex because it depends on Komponist.
                # We use the k_id we just resolved.
                # But we should also normalize/map the Werk Name itself?
                # User did not explicitly ask for Werk normalization, only Dirigent, Komponist, Solist, Orchester, Ort.
                # But consistent behavior suggests we should at least check the map?
                # The user list "Dirigent, Komponist, Solist, Orchester, Ort" excluded Werk. I will stick to that.

                werk_key = f"{row['Werk']}_{k_id}"
                # We do NOT normalize Werk names per user scope, but we use the resolved k_id.
                # However, get_or_create expects a cache key. We can use the raw key.
                # Note: get_or_create logic above normalizes the key!
                # If I pass 'Werk_123', normalize_name might mess it up?
                # normalize_name("Missa_123") -> "missa_123". That's fine.

                w_id = self.get_or_create('werk', werk_key, 
                    "INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)", (row['Werk'], k_id))

            # 3. MusicRecord Hauptdatensatz (OHNE die alten Spalten 'Werk'/'Komponist')
            bewertung = f"{row['Bewertung1']}\n{row['Bewertung2']}".strip()
            sql_mr = """INSERT INTO MusicRecords 
                        (Id, Bezeichnung, Datum, Spielsaison, Bewertung, OrtId, DirigentId, OrchesterId)
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
            
            # Als 'Bezeichnung' nehmen wir den Namen des Werks
            self.new_cur.execute(sql_mr, (
                row['Id'], row['Werk'], row['Datum'], row['Spielsaison'], 
                bewertung, ort_id, d_id, o_id
            ))

            # 4. n:m Beziehung: MusicRecord <-> Werk
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
