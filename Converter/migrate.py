import mysql.connector
from mysql.connector import Error
import argparse
import sys
import os
import unicodedata
import re
import json
from openai import OpenAI
from dotenv import load_dotenv

load_dotenv()

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
        # Lookup for "Name only" ambiguity resolution
        # Structure: { category: { normalized_lastname: [set of normalized_fullnames] } }
        self.name_index = {'dirigent': {}, 'komponist': {}, 'solist': {}}
        self.correction_map = {}
        self.todo_counter = 1

        # AI Setup
        self.api_key = os.getenv("AI_API_KEY")
        self.client = OpenAI(api_key=self.api_key) if self.api_key else None
        if not self.client:
             print("⚠️ Warning: No AI_API_KEY found. AI features will be skipped.")

    def analyze_with_ai(self, komponist, werk, bewertung):
        if not self.client: return None

        prompt = f"""
        Analyze the following classical music record and extract a structured list of composers and their works.

        Input Data:
        - Komponist (Composer field): "{komponist}"
        - Werk (Work field): "{werk}"
        - Bemerkung (Notes): "{bewertung}"

        Instructions:
        1. The 'Komponist' field may contain multiple names (separated by /, comma, 'und') or 'u.a.' (meaning 'and others').
        2. If 'u.a.' is present, or multiple names are listed, check 'Bemerkung' for details on other works performed.
        3. Return a JSON object with a single key "items" containing a list of objects.
        4. Each object must have:
           - "Komponist": The full name of the composer.
           - "Werk": The title of the work.
        5. If the 'Werk' field applies to the first composer, include it.

        Example JSON Output:
        {{
          "items": [
            {{ "Komponist": "Ludwig van Beethoven", "Werk": "Symphonie Nr. 5" }},
            {{ "Komponist": "Wolfgang Amadeus Mozart", "Werk": "Ouvertüre zu Die Zauberflöte" }}
          ]
        }}
        """

        try:
            response = self.client.chat.completions.create(
                model="gpt-4o",
                messages=[
                    {"role": "system", "content": "You are a helpful assistant that extracts structured data from legacy database records. Output valid JSON."},
                    {"role": "user", "content": prompt}
                ],
                response_format={"type": "json_object"}
            )
            content = response.choices[0].message.content
            data = json.loads(content)
            return data.get("items", [])
        except Exception as e:
            print(f"❌ AI Error: {e}")
            return None

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

        pattern = r"\s*[/,]\s*|\s+und\s+"
        parts = re.split(pattern, val, flags=re.IGNORECASE)

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
        print("🔍 Analysiere Daten auf Duplikate und Ambiguities...")

        # 1. Pass: Collect all normalized names to detect ambiguities
        # We need to scan all data to build the name index FIRST
        # because "Müller" might appear before "Thomas Müller".

        temp_name_index = {'dirigent': {}, 'komponist': {}, 'solist': {}}
        all_values = {'dirigent': [], 'komponist': [], 'solist': [], 'orchester': [], 'ort': []} # To avoid re-reading DB cursor if possible, or just store sets?

        # We'll read all into memory (dataset size seems manageable given context)
        self.old_cur.execute("SELECT * FROM MusicRecords")
        old_data = self.old_cur.fetchall()

        # Helper to collect full names
        def collect_full_names(category, val):
            if not val: return
            norm = self.normalize_name(val)
            if not norm: return

            # Store raw val for later duplicate check
            all_values[category].append(val)

            # If category supports name/vorname split
            if category in ['dirigent', 'komponist', 'solist']:
                first, last = self.split_name(val)
                if first: # Has a first name
                    norm_last = self.normalize_name(last)
                    if norm_last not in temp_name_index[category]:
                        temp_name_index[category][norm_last] = set()
                    temp_name_index[category][norm_last].add(norm) # Store full normalized name

        # Pass 1: Build Index
        for row in old_data:
            collect_full_names('dirigent', row['Dirigent'])
            # Komponist split
            for k in self.split_komponist_string(row['Komponist']):
                collect_full_names('komponist', k)
            # Solist split
            if row['Solist']:
                solist_clean = row['Solist'].replace("u.a.", "").replace("u. a.", "").strip()
                for s in [x.strip() for x in solist_clean.split(',')]:
                    collect_full_names('solist', s)

            # Others (just for duplicate check later)
            if row['Orchester']: all_values['orchester'].append(row['Orchester'])
            if row['Ort']: all_values['ort'].append(row['Ort'])

        duplicates_map = {}
        seen_normalized = {c: {} for c in all_values.keys()}

        # Pass 2: Detect Duplicates and Ambiguities
        def check_value(category, val):
            if not val: return
            norm = self.normalize_name(val)
            if not norm: return

            # A. Check "Name Only" Ambiguity
            if category in ['dirigent', 'komponist', 'solist']:
                first, last = self.split_name(val)
                if not first and last: # Name only
                    norm_last = self.normalize_name(last)
                    possible_matches = temp_name_index[category].get(norm_last, set())

                    if len(possible_matches) > 1:
                        # AMBIGUOUS
                         duplicates_map[val] = f"AMBIGUOUS: {' | '.join(sorted(list(possible_matches)))}"
                         return # Skip standard duplicate check? Or continue?
                         # If ambiguous, we don't know which one it is, so we can't really "deduplicate" automatically.
                    elif len(possible_matches) == 1:
                         # UNIQUE MATCH
                         # We can suggest mapping "Müller" -> "Thomas Müller"
                         match = list(possible_matches)[0]
                         # We need the original string of the match? 'match' is normalized.
                         # This is tricky. We stored normalized strings in index.
                         # The map expects { "Bad": "Good" }.
                         # If we map "Müller" -> "thomas muller", the next run will normalize "thomas muller" and find ID.
                         # This is acceptable? Yes.
                         if val not in duplicates_map:
                             duplicates_map[val] = match
                         return

            # B. Standard Deduplication
            if norm in seen_normalized[category]:
                first_val = seen_normalized[category][norm]
                if first_val != val:
                     if val not in duplicates_map:
                         duplicates_map[val] = first_val
            else:
                seen_normalized[category][norm] = val

        # Iterate again over the collected values (preserving order effectively)
        for cat, vals in all_values.items():
            for v in vals:
                check_value(cat, v)

        print(f"✓ Analyse beendet. {len(duplicates_map)} Einträge für Map gefunden.")

        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                # Convert sets to lists if any slipped in (shouldn't happen with current logic)
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

    def update_name_index(self, category, full_name, db_id):
        """
        Updates the runtime name index when a new valid record with First+Last name is added.
        """
        if category not in self.name_index: return
        first, last = self.split_name(full_name)
        if first and last:
            norm_last = self.normalize_name(last)
            if norm_last not in self.name_index[category]:
                self.name_index[category][norm_last] = []

            # Store tuple of (normalized_full_name, db_id)
            norm_full = self.normalize_name(full_name)
            self.name_index[category][norm_last].append((norm_full, db_id))

    def get_or_create(self, cache_key, lookup_val, insert_sql, params):
        if not lookup_val: return None

        # 1. Apply Correction Map
        val_to_use = self.correction_map.get(lookup_val, lookup_val)

        # 2. Normalize
        norm_key = self.normalize_name(val_to_use)

        # 3. Check Cache (using normalized key)
        if norm_key in self.cache[cache_key]:
            return self.cache[cache_key][norm_key]

        # 3b. Name-Only Lookup Logic (if not in cache)
        if cache_key in ['dirigent', 'komponist', 'solist']:
             first, last = self.split_name(val_to_use)
             if not first and last: # No first name
                 norm_last = self.normalize_name(last)
                 candidates = self.name_index[cache_key].get(norm_last, [])

                 # Logic: If exactly one match, use it.
                 # Note: candidates contains (norm_full, id)
                 # We need to filter duplicates in candidates list (same person might be added twice if logic fails elsewhere,
                 # but actually we just want unique IDs or unique Names?)
                 # Use a set of unique IDs found
                 unique_ids = list({c[1] for c in candidates})

                 if len(unique_ids) == 1:
                     # Found unique match! Use this ID.
                     # Also cache this "Short Name" -> ID mapping so next time it's fast
                     found_id = unique_ids[0]
                     self.cache[cache_key][norm_key] = found_id
                     return found_id

                 # If > 1: Ambiguous. We do nothing special, proceed to create new entry (or rely on later manual fix).
                 # If 0: No match. Create new.

        # 4. Insert (using the potentially corrected original value 'val_to_use')
        final_params = params
        if val_to_use != lookup_val:
            if cache_key in ['dirigent', 'solist', 'komponist']:
                parts = self.split_name(val_to_use)
                if cache_key == 'komponist':
                    final_params = (*parts, "")
                else:
                    final_params = parts
            elif cache_key in ['orchester', 'ort']:
                final_params = (val_to_use,)
            elif cache_key == 'werk':
               pass

        self.new_cur.execute(insert_sql, final_params)
        new_id = self.new_cur.lastrowid
        self.cache[cache_key][norm_key] = new_id

        # Update name index with the new entry
        self.update_name_index(cache_key, val_to_use, new_id)

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
                
                # Check for AI triggers
                k_raw = row['Komponist'] or ""
                has_ua = "u.a." in k_raw.lower() or "u. a." in k_raw.lower()

                ai_done = False

                if (len(komp_strings) > 1 or has_ua) and self.client:
                    # Trigger AI
                    bewertung_ai = row['Bewertung1'] or ""
                    print(f"🤖 AI Request for ID {row['Id']}: {k_raw} | {row['Werk']}")
                    ai_items = self.analyze_with_ai(k_raw, row['Werk'], bewertung_ai)

                    if ai_items:
                        ai_done = True
                        for item in ai_items:
                            ai_k = item.get("Komponist")
                            ai_w = item.get("Werk")

                            if ai_k and ai_w:
                                # Create/Find Komponist
                                k_id = self.get_or_create('komponist', ai_k,
                                    "INSERT INTO Komponisten (Vorname, Name, Note) VALUES (%s, %s, %s)",
                                    (*self.split_name(ai_k), ""))

                                # Create/Find Werk
                                werk_key = f"{ai_w}_{k_id}"
                                w_id = self.get_or_create('werk', werk_key,
                                    "INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)", (ai_w, k_id))
                                w_ids.append(w_id)
                        print(f"   -> AI found {len(ai_items)} items.")
                    else:
                        print("   -> AI returned no results. Falling back.")

                if not ai_done:
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
