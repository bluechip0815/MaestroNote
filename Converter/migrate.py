import mysql.connector
from mysql.connector import Error
import argparse
import sys
import os
import unicodedata
import re
import logging
from dotenv import load_dotenv

load_dotenv()

# ============================================================
# LOGGING CONFIGURATION
# ============================================================
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("migration.log", mode='w', encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

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
    def __init__(self, interactive=False, connect=True):
        self.interactive = interactive
        if connect:
            try:
                self.old_conn = mysql.connector.connect(**DB_CONFIG["old"])
                self.old_cur = self.old_conn.cursor(dictionary=True)
                logging.info("✓ Alte Datenbank verbunden.")

                self.new_conn = mysql.connector.connect(**DB_CONFIG["new"])
                self.new_cur = self.new_conn.cursor()
                logging.info("✓ Neue Datenbank verbunden.")

            except Error as e:
                logging.error(f"❌ Verbindungsfehler: {e}")
                sys.exit(1)
        else:
            self.old_conn = None
            self.old_cur = None
            self.new_conn = None
            self.new_cur = None

        # Caching um Duplikate in den Stammdaten zu vermeiden
        # Key: Normalized String, Value: ID in DB
        self.cache = {'dirigent': {}, 'orchester': {}, 'komponist': {}, 'werk': {}, 'solist': {}, 'ort': {}}

        # Load existing data from New DB
        self.load_existing_data()

    def normalize_name(self, name):
        if not name: return ""
        s = name.lower()
        s = ''.join(c for c in unicodedata.normalize('NFD', s) if unicodedata.category(c) != 'Mn')
        s = re.sub(r'\bv\.?\s+', 'van ', s)
        return s.strip()

    def split_name(self, full_name):
        if not full_name: return "", ""
        if ';' in full_name:
             parts = full_name.split(';')
             if len(parts) >= 2:
                 return parts[0].strip(), parts[1].strip()
             return parts[0].strip(), ""
        parts = full_name.strip().split(' ')
        if len(parts) == 1: return "", parts[0]
        return " ".join(parts[:-1]), parts[-1]

    def split_komponist_string(self, val):
        if not val: return []
        pattern = r"\s*[/,]\s*|\s+und\s+"
        parts = re.split(pattern, val, flags=re.IGNORECASE)
        return [p.strip() for p in parts if p.strip()]

    def parse_complex_entity_string(self, raw_val, category):
        """
        Parses a raw string (e.g. "Beethoven, Mozart u.a.") into valid entity names.
        Simplified version without correction map.
        """
        if not raw_val: return []

        # 1. Clean
        clean_val = raw_val
        for junk in ["u.a.", "u. a.", "etc."]:
            clean_val = re.sub(re.escape(junk), "", clean_val, flags=re.IGNORECASE)

        # 2. Split by comma and slash
        primary_parts = re.split(r'[,/]', clean_val)

        resolved_names = []
        for segment in primary_parts:
            seg = segment.strip()
            if seg:
                resolved_names.append(seg)

        return resolved_names

    def clear_new_db(self):
        logging.info("⚠️ Lösche neue Datenbank für frischen Import...")
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

    def clear_works(self):
        logging.info("⚠️ Lösche Werke und Verknüpfungen...")
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 0;")
        try:
            self.new_cur.execute("TRUNCATE TABLE MusicRecordWerk;")
            self.new_cur.execute("TRUNCATE TABLE Werke;")
            logging.info("✓ Werke tables truncated.")
        except Error as e:
            logging.error(f"❌ Error truncating works: {e}")
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 1;")
        self.new_conn.commit()

    def load_existing_data(self):
        """Loads existing entities from the new database into the cache."""
        logging.info("Loading existing data from new database...")

        def load_simple(table, cache_key):
            try:
                self.new_cur.execute(f"SELECT Id, Name FROM {table}")
                for row in self.new_cur.fetchall():
                    norm = self.normalize_name(row[1])
                    if norm:
                        self.cache[cache_key][norm] = row[0]
            except Exception as e:
                logging.error(f"Error loading {table}: {e}")

        def load_person(table, cache_key):
            try:
                self.new_cur.execute(f"SELECT Id, Vorname, Name FROM {table}")
                for row in self.new_cur.fetchall():
                    full_name = f"{row[1]} {row[2]}".strip()
                    norm = self.normalize_name(full_name)
                    if norm:
                        self.cache[cache_key][norm] = row[0]
            except Exception as e:
                logging.error(f"Error loading {table}: {e}")

        load_person("Dirigenten", "dirigent")
        load_person("Komponisten", "komponist")
        load_person("Solisten", "solist")
        load_simple("Orchester", "orchester")
        load_simple("Orte", "ort")

        # Werke
        try:
            self.new_cur.execute("SELECT Id, Name, KomponistId FROM Werke")
            for row in self.new_cur.fetchall():
                key = f"{row[1]}_{row[2]}"
                norm = self.normalize_name(key)
                if norm:
                    self.cache['werk'][norm] = row[0]
        except Exception as e:
            logging.error(f"Error loading Werke: {e}")

    def ensure_entity(self, category, value, insert_sql, params):
        """
        Simple entity ensuring: Check Cache -> Insert if missing.
        """
        if not value: return None
        norm_key = self.normalize_name(value)

        # Cache Lookup
        if norm_key in self.cache[category]:
            return self.cache[category][norm_key]

        # Insert
        try:
            self.new_cur.execute(insert_sql, params)
            new_id = self.new_cur.lastrowid
            self.new_conn.commit()
            self.cache[category][norm_key] = new_id
            logging.info(f"Created {category}: {value}")
            return new_id
        except Error as e:
            logging.error(f"Failed to insert {category} '{value}': {e}")
            return None

    def lookup_entity(self, category, value):
        if not value: return None
        norm_key = self.normalize_name(value)
        return self.cache[category].get(norm_key)

    # ============================================================
    # ENTITY MIGRATION STUBS
    # ============================================================

    def migrate_solists(self):
        logging.info("Starting Solist migration...")
        self.old_cur.execute("SELECT DISTINCT Solist FROM MusicRecords WHERE Solist IS NOT NULL AND Solist != ''")
        count = 0
        for row in self.old_cur.fetchall():
            val = row['Solist']
            resolved = self.parse_complex_entity_string(val, 'solist')
            for p in resolved:
                if self.ensure_entity('solist', p,
                    "INSERT INTO Solisten (Vorname, Name) VALUES (%s, %s)",
                    self.split_name(p)):
                    count += 1
        logging.info(f"Solist migration finished. Processed {count} entries.")

    def migrate_composers(self):
        logging.info("Starting Composer migration...")
        self.old_cur.execute("SELECT DISTINCT Komponist FROM MusicRecords WHERE Komponist IS NOT NULL AND Komponist != ''")
        count = 0
        for row in self.old_cur.fetchall():
            val = row['Komponist']
            resolved = self.parse_complex_entity_string(val, 'komponist')
            for p in resolved:
                if self.ensure_entity('komponist', p,
                    "INSERT INTO Komponisten (Vorname, Name, Note) VALUES (%s, %s, %s)",
                    (*self.split_name(p), "")):
                    count += 1
        logging.info(f"Composer migration finished. Processed {count} entries.")

    def migrate_conductors(self):
        logging.info("Starting Conductor migration...")
        self.old_cur.execute("SELECT DISTINCT Dirigent FROM MusicRecords WHERE Dirigent IS NOT NULL AND Dirigent != ''")
        count = 0
        for row in self.old_cur.fetchall():
            val = row['Dirigent']
            resolved = self.parse_complex_entity_string(val, 'dirigent')
            for p in resolved:
                if self.ensure_entity('dirigent', p,
                    "INSERT INTO Dirigenten (Vorname, Name) VALUES (%s, %s)",
                    self.split_name(p)):
                    count += 1
        logging.info(f"Conductor migration finished. Processed {count} entries.")

    def migrate_orchestras(self):
        logging.info("Starting Orchestra migration...")
        self.old_cur.execute("SELECT DISTINCT Orchester FROM MusicRecords WHERE Orchester IS NOT NULL AND Orchester != ''")
        count = 0
        for row in self.old_cur.fetchall():
            val = row['Orchester']
            if self.ensure_entity('orchester', val,
                "INSERT INTO Orchester (Name) VALUES (%s)",
                (val,)):
                count += 1
        logging.info(f"Orchestra migration finished. Processed {count} entries.")

    def migrate_locations(self):
        logging.info("Starting Location migration...")
        self.old_cur.execute("SELECT DISTINCT Ort FROM MusicRecords WHERE Ort IS NOT NULL AND Ort != ''")
        count = 0
        for row in self.old_cur.fetchall():
            val = row['Ort']
            if self.ensure_entity('ort', val,
                "INSERT INTO Orte (Name) VALUES (%s)",
                (val,)):
                count += 1
        logging.info(f"Location migration finished. Processed {count} entries.")

    def get_composer_id(self, name_str):
        if not name_str: return None

        # Check specific mappings
        if name_str == "CSchumann":
            try:
                self.new_cur.execute("SELECT Id FROM Komponisten WHERE Name = 'Schumann' AND Vorname = 'Clara'")
                res = self.new_cur.fetchone()
                if res: return res[0]
            except Error as e:
                logging.error(f"Error looking up Clara Schumann: {e}")
            return None

        if name_str == "Schumann":
             try:
                 # Check for Robert explicitly
                 self.new_cur.execute("SELECT Id FROM Komponisten WHERE Name = 'Schumann' AND Vorname LIKE '%Robert%'")
                 res = self.new_cur.fetchone()
                 if res: return res[0]
             except Error as e:
                 logging.error(f"Error looking up Robert Schumann: {e}")
             # If not found, fall through to generic lookup?
             # Or return None? 'Schumann' usually implies Robert here.
             return None

        # Generic Lookup by Name
        try:
            self.new_cur.execute("SELECT Id FROM Komponisten WHERE Name = %s", (name_str,))
            matches = self.new_cur.fetchall()
            if len(matches) >= 1:
                return matches[0][0]
        except Error as e:
            logging.error(f"Error looking up composer '{name_str}': {e}")

        return None

    def resolve_work(self, composer_id, work_name):
        """
        Ensures a work exists for the given composer.
        """
        try:
            self.new_cur.execute("SELECT Id FROM Werke WHERE Name = %s AND KomponistId = %s", (work_name, composer_id))
            res = self.new_cur.fetchone()
            if res:
                return res[0]

            # Insert
            self.new_cur.execute("INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)", (work_name, composer_id))
            self.new_conn.commit()
            new_id = self.new_cur.lastrowid

            # Update cache
            werk_key = f"{work_name}_{composer_id}"
            self.cache['werk'][self.normalize_name(werk_key)] = new_id

            return new_id
        except Error as e:
            logging.error(f"Error resolving Werk '{work_name}' for Composer ID {composer_id}: {e}")
            return None

    def migrate_works(self):
        logging.info("Starting Work migration...")
        # Get source works
        self.old_cur.execute("SELECT DISTINCT Werk, Komponist FROM MusicRecords WHERE Werk IS NOT NULL AND Werk != ''")

        count = 0
        skipped = 0

        for row in self.old_cur.fetchall():
            werk_name = row['Werk']
            komp_raw = row['Komponist']

            if not komp_raw:
                skipped += 1
                continue

            # Parse Composer String
            composer_names = []
            if ',' in komp_raw:
                # Multiple composers
                composer_names = [x.strip() for x in komp_raw.split(',') if x.strip()]
            else:
                # Single composer -> take last part
                parts = komp_raw.strip().split(' ')
                if parts:
                    composer_names = [parts[-1].strip()]

            if not composer_names:
                skipped += 1
                continue

            for c_name in composer_names:
                c_id = self.get_composer_id(c_name)

                if not c_id:
                    logging.warning(f"Composer '{c_name}' (from '{komp_raw}') not found in destination.")
                    skipped += 1
                    continue

                # Function Call to resolve/create work
                if self.resolve_work(c_id, werk_name):
                    count += 1
                else:
                    skipped += 1

        logging.info(f"Work migration finished. Processed/Inserted: {count}, Skipped/Issues: {skipped}.")

    def migrate_records(self):
        logging.info("Starting MusicRecords migration (without Documents)...")
        self.old_cur.execute("SELECT * FROM MusicRecords")
        old_data = self.old_cur.fetchall()

        success_count = 0
        skipped_count = 0

        for row in old_data:
            # 1. Resolve Dependencies
            missing_deps = []

            # Dirigent
            d_id = None
            if row['Dirigent']:
                d_id = self.lookup_entity('dirigent', row['Dirigent'])
                if not d_id: missing_deps.append(f"Dirigent: {row['Dirigent']}")

            # Orchester
            o_id = None
            if row['Orchester']:
                o_id = self.lookup_entity('orchester', row['Orchester'])
                if not o_id: missing_deps.append(f"Orchester: {row['Orchester']}")

            # Ort
            ort_id = None
            if row['Ort']:
                ort_id = self.lookup_entity('ort', row['Ort'])
                if not ort_id: missing_deps.append(f"Ort: {row['Ort']}")

            # Solisten
            s_ids = []
            if row['Solist']:
                solist_clean = row['Solist'].replace("u.a.", "").replace("u. a.", "").strip()
                parts = [s.strip() for s in solist_clean.split(',') if s.strip()]
                for p in parts:
                    s_id = self.lookup_entity('solist', p)
                    if s_id:
                        s_ids.append(s_id)
                    else:
                        missing_deps.append(f"Solist: {p}")

            # Werke
            w_ids = []
            bezeichnung = row['Werk'] # Default designation

            if row['Werk']:
                # Simple parsing for records?
                # Original logic tried to match "Name_ComposerId".
                # But `row['Komponist']` is a string.
                # We need to find the composer ID first.
                # This part is tricky without the map if the composer string in record doesn't match cache.
                # But assuming `migrate_works` and `migrate_composers` ran, cache should be populated.

                # We need to parse the composer string from the record
                komp_parts = self.split_komponist_string(row['Komponist'])
                if not komp_parts:
                     missing_deps.append(f"Werk (No Composer): {row['Werk']}")
                else:
                    first_komp_str = komp_parts[0]
                    # Try to find composer.
                    # Note: lookup_entity uses normalized full string match against cache.
                    k_id = self.lookup_entity('komponist', first_komp_str)

                    if not k_id:
                         # Fallback: Try "Right Part" lookup if exact match fails?
                         # The legacy records might use "Beethoven" but we stored "Ludwig van Beethoven".
                         # `lookup_entity` only checks exact normalized match in cache.
                         # But `load_existing_data` only keys by Full Name.
                         # If we want `migrate_records` to work well, we might need that "Name Index" or fuzzy search.
                         # But for now, I'm sticking to "Remove correction map" and "Add work features".
                         # I won't overengineer `migrate_records` unless asked.
                         missing_deps.append(f"Composer for Work: {first_komp_str}")
                    else:
                        werk_key = f"{row['Werk']}_{k_id}"
                        w_id = self.lookup_entity('werk', werk_key)
                        if w_id:
                            w_ids.append(w_id)
                        else:
                            missing_deps.append(f"Werk: {row['Werk']} ({first_komp_str})")
            
            if missing_deps:
                logging.error(f"Record {row['Id']} skipped. Missing: {', '.join(missing_deps)}")
                skipped_count += 1
                continue

            # NEW: Prepare data for insertion/comparison
            bewertung = f"{row['Bewertung1']}\n{row['Bewertung2']}".strip()
            # New record data structure
            new_record_data = {
                'Datum': row['Datum'],
                'Spielsaison': row['Spielsaison'],
                'Bewertung': bewertung,
                'OrtId': ort_id,
                'DirigentId': d_id,
                'OrchesterId': o_id,
                'Bezeichnung': bezeichnung,
                'Werke': set(w_ids),
                'Solisten': set(s_ids)
            }

            # 2. Date Check & Comparison
            try:
                self.new_cur.execute("SELECT Id, Spielsaison, Bewertung, OrtId, DirigentId, OrchesterId FROM MusicRecords WHERE Datum = %s", (row['Datum'],))
                existing = self.new_cur.fetchall()

                if len(existing) > 1:
                    logging.error(f"Record {row['Id']} skipped. Multiple records exist for date {row['Datum']}.")
                    skipped_count += 1
                    continue

                if len(existing) == 1:
                    ex_row = existing[0]
                    ex_id = ex_row[0]

                    # Fetch relations for comparison
                    self.new_cur.execute("SELECT WerkeId FROM MusicRecordWerk WHERE MusicRecordsId = %s", (ex_id,))
                    ex_w_ids = {r[0] for r in self.new_cur.fetchall()}

                    self.new_cur.execute("SELECT SolistenId FROM MusicRecordSolist WHERE MusicRecordsId = %s", (ex_id,))
                    ex_s_ids = {r[0] for r in self.new_cur.fetchall()}

                    def norm_str(s): return (s or "").strip()

                    ex_data = {
                        'Spielsaison': ex_row[1],
                        'Bewertung': ex_row[2],
                        'OrtId': ex_row[3],
                        'DirigentId': ex_row[4],
                        'OrchesterId': ex_row[5],
                        'Werke': ex_w_ids,
                        'Solisten': ex_s_ids
                    }

                    # Compare
                    is_diff = False
                    if norm_str(ex_data['Spielsaison']) != norm_str(new_record_data['Spielsaison']): is_diff = True
                    if norm_str(ex_data['Bewertung']) != norm_str(new_record_data['Bewertung']): is_diff = True
                    if ex_data['OrtId'] != new_record_data['OrtId']: is_diff = True
                    if ex_data['DirigentId'] != new_record_data['DirigentId']: is_diff = True
                    if ex_data['OrchesterId'] != new_record_data['OrchesterId']: is_diff = True
                    if ex_data['Werke'] != new_record_data['Werke']: is_diff = True
                    if ex_data['Solisten'] != new_record_data['Solisten']: is_diff = True

                    if not is_diff:
                        logging.info(f"Skipping identical record for {row['Datum']}")
                        continue

                    # Conflict
                    do_overwrite = False
                    if self.interactive:
                        print(f"\n⚠️ Conflict for Date {row['Datum']}:")
                        print(f"   Existing: {ex_data}")
                        print(f"   New:      {new_record_data}")
                        choice = input("(s)kip, (o)verwrite? ").strip().lower()
                        if choice == 'o': do_overwrite = True
                        else: logging.info(f"Skipping conflict for {row['Datum']}")
                    else:
                        logging.info(f"Skipping conflict (non-interactive) for {row['Datum']}")
                        continue # Skip

                    if do_overwrite:
                        self.new_cur.execute("DELETE FROM MusicRecords WHERE Id = %s", (ex_id,))
                        logging.info(f"Deleted existing record {ex_id} for overwrite.")
                    else:
                        continue

            except Error as e:
                 logging.error(f"Database error during date check/delete for {row['Id']}: {e}")
                 skipped_count += 1
                 continue

            # 3. Insert Record
            try:
                sql_mr = """INSERT INTO MusicRecords
                            (Id, Bezeichnung, Datum, Spielsaison, Bewertung, OrtId, DirigentId, OrchesterId)
                            VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""

                self.new_cur.execute(sql_mr, (
                    row['Id'], bezeichnung, row['Datum'], row['Spielsaison'],
                    bewertung, ort_id, d_id, o_id
                ))

                # Links
                for w_id in w_ids:
                    self.new_cur.execute("INSERT INTO MusicRecordWerk (MusicRecordsId, WerkeId) VALUES (%s, %s)", (row['Id'], w_id))

                for s_id in s_ids:
                    self.new_cur.execute("INSERT INTO MusicRecordSolist (MusicRecordsId, SolistenId) VALUES (%s, %s)", (row['Id'], s_id))

                self.new_conn.commit()
                success_count += 1

            except Error as e:
                logging.error(f"Failed to insert Record {row['Id']}: {e}")
                skipped_count += 1

        logging.info(f"Records migration finished. Success: {success_count}, Skipped: {skipped_count}")

    def migrate_documents(self):
        logging.info("Starting Documents migration...")
        self.old_cur.execute("SELECT * FROM Documents")
        docs = self.old_cur.fetchall()

        count = 0
        skipped = 0

        for doc in docs:
            # Check if MusicRecord exists
            self.new_cur.execute("SELECT Id FROM MusicRecords WHERE Id = %s", (doc['MusicRecordId'],))
            res = self.new_cur.fetchone()

            if not res:
                logging.warning(f"Document {doc['Id']} skipped: MusicRecord {doc['MusicRecordId']} not found.")
                skipped += 1
                continue

            try:
                self.new_cur.execute(
                    "INSERT INTO Documents (Id, FileName, EncryptedName, DocumentType, MusicRecordId) VALUES (%s, %s, %s, %s, %s)",
                    (doc['Id'], doc['FileName'], doc['EncryptedName'], doc['DocumentType'], doc['MusicRecordId'])
                )
                count += 1
            except Error as e:
                logging.error(f"Failed to insert Document {doc['Id']}: {e}")
                skipped += 1

        self.new_conn.commit()
        logging.info(f"Documents migration finished. Processed {count}, Skipped {skipped}.")

    def clear_documents(self):
        logging.info("Clearing Documents table...")
        try:
            self.new_cur.execute("TRUNCATE TABLE Documents")
            self.new_conn.commit()
            logging.info("Documents table cleared.")
        except Error as e:
            logging.error(f"Failed to clear Documents: {e}")

    def clear_notes(self):
        logging.info("Clearing Notes (Bewertung) in MusicRecords...")
        try:
            self.new_cur.execute("UPDATE MusicRecords SET Bewertung = ''")
            self.new_conn.commit()
            logging.info("Notes cleared.")
        except Error as e:
            logging.error(f"Failed to clear Notes: {e}")

    def close(self):
        self.old_cur.close()
        self.old_conn.close()
        if self.new_cur: self.new_cur.close()
        if self.new_conn: self.new_conn.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Migration Tool")

    # Actions
    parser.add_argument('--add-solist', action='store_true', help="Migrate Solists")
    parser.add_argument('--add-composer', action='store_true', help="Migrate Composers")
    parser.add_argument('--add-work', action='store_true', help="Migrate Works")
    parser.add_argument('--del-work', action='store_true', help="Delete all Works and Links")
    parser.add_argument('--add-conductor', action='store_true', help="Migrate Conductors")
    parser.add_argument('--add-orchestra', action='store_true', help="Migrate Orchestras")
    parser.add_argument('--add-location', action='store_true', help="Migrate Locations")
    parser.add_argument('--notes', action='store_true', help="Migrate MusicRecords (without Documents)")
    parser.add_argument('--documents', action='store_true', help="Migrate Documents table")
    parser.add_argument('--delete-doc', action='store_true', help="Clear Document table")
    parser.add_argument('--delete-note', action='store_true', help="Remove Bemerkung only")

    # Options
    parser.add_argument('--interactive', action='store_true', help="Enable interactive mode for ambiguities")
    parser.add_argument('--delete', action='store_true', help="Clear destination DB before starting")

    args = parser.parse_args()
    
    spec = MigrationSpecialist(interactive=args.interactive)

    if args.delete:
        spec.clear_new_db()

    if args.delete_doc:
        spec.clear_documents()

    if args.delete_note:
        spec.clear_notes()

    if args.del_work:
        spec.clear_works()

    if args.add_solist:
        spec.migrate_solists()

    if args.add_composer:
        spec.migrate_composers()

    if args.add_conductor:
        spec.migrate_conductors()

    if args.add_orchestra:
        spec.migrate_orchestras()

    if args.add_location:
        spec.migrate_locations()

    if args.add_work:
        spec.migrate_works()

    if args.notes:
        spec.migrate_records()

    if args.documents:
        spec.migrate_documents()

    spec.close()
