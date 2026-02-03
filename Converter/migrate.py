import mysql.connector
from mysql.connector import Error
import argparse
import sys
import os
import unicodedata
import re
import json
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
        # Lookup for "Name only" ambiguity resolution
        self.name_index = {'dirigent': {}, 'komponist': {}, 'solist': {}}
        self.correction_map = {}
        self.todo_counter = 1

        # Load existing data from New DB
        self.load_existing_data()

        # Load correction map by default
        self.load_correction_map(os.path.join(os.path.dirname(__file__), 'correction-map.json'))

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
        Parses a raw string (e.g. "Beethoven, Mozart u.a.") into valid entity names
        using the correction map and splitting logic.
        Returns a list of resolved names (mapped values).
        Logs unknowns.
        """
        if not raw_val: return []

        # 1. Clean
        # Remove "u.a.", "u. a.", "etc."
        clean_val = raw_val
        for junk in ["u.a.", "u. a.", "etc."]:
            clean_val = re.sub(re.escape(junk), "", clean_val, flags=re.IGNORECASE)

        # 2. Split by comma and slash
        primary_parts = re.split(r'[,/]', clean_val)

        resolved_names = []

        for segment in primary_parts:
            seg = segment.strip()
            if not seg: continue

            # 3. Split by Space
            parts = seg.split(' ')
            parts = [p.strip() for p in parts if p.strip()]

            if not parts: continue

            # Check Longest Match / Combinations
            # User requirement: "then the combination of parts will be tested"
            # User example: "Ludwig van Beethoven" -> finds "Beethoven".
            # This implies we check individual parts, but also potentially combinations?
            # User said: "if there three parts you will find 'Beethoven'..."
            # This strongly implies checking each part individually against the map.
            # But what if the map has "Ludwig van Beethoven"?
            # If we check only "Ludwig", "van", "Beethoven", we miss the full key.
            # So we should try the Whole Segment first.

            # Strategy:
            # A. Check Whole Segment (e.g. "Ludwig van Beethoven")
            if seg in self.correction_map:
                resolved_names.append(self.correction_map[seg])
                continue

            # B. Check parts
            # "Ludwig", "van", "Beethoven"
            found_any_in_segment = False

            for p in parts:
                if p in self.correction_map:
                    resolved_names.append(self.correction_map[p])
                    found_any_in_segment = True
                else:
                    # Log unknown parts as requested
                    # "if nothing is found, write to the log, I will add the info"
                    # But we only log if it's TRULY unknown (not part of a bigger match we missed?)
                    # Given the user instruction, we treat every space-split part as a candidate.
                    # If "Ludwig" is not in map, we log it.
                    logging.info(f"ℹ️ Info: Part '{p}' from '{seg}' not found in correction map.")

            # If we didn't find ANY match in the segment via map,
            # we might want to return the segment itself as a candidate for DB lookup?
            # The prompt says: "when entry in correction table is found then the corrected value is used"
            # It doesn't explicitly say "only use correction table".
            # But "if nothing is found, write to the log".
            # This implies strict reliance on Map for this parsing step?
            # If I return nothing, `ensure_entity` won't be called.
            # But maybe the user implies: If "Beethoven" is found, use it.
            # If "Unknown" is not found, log it.
            # Does "Unknown" get added?
            # "if nothing is found... I will add the info" -> Implies manual fix later.
            # So for now, we only yield matches?
            # BUT: If I have a valid new composer "John Smith" (not in map),
            # I want to add him!
            # So we should probably treat the segment as a fallback if no parts matched?

            if not found_any_in_segment:
                # If no parts were mapped, we assume the whole segment is the name
                # (e.g. "John Smith" - no map entry, but valid name).
                # We add it to resolved list so ensure_entity can process it (DB lookup, Auto-Accept, etc.)
                resolved_names.append(seg)

        return resolved_names

    def validate_and_fix_input(self, category, value, authorized=False):
        """
        Validates input based on rules:
        - Skip 'etc.', 'u.a.'
        - Min length 4 chars
        - Person types must have First and Last Name (UNLESS authorized)
        Returns cleaned value or None (if skipped).
        Handles interactive prompting.
        """
        current_val = value

        while True:
            if not current_val: return None

            # 1. Blacklist Check
            lower_val = current_val.lower().strip()
            if lower_val in ['etc.', 'u.a.', 'u. a.'] or lower_val.endswith('etc.'):
                logging.info(f"Skipping junk entry: '{current_val}'")
                return None

            # 2. Length Check
            if len(current_val) < 4:
                msg = f"Value '{current_val}' is too short (<4 chars)."
                if self.interactive:
                    print(f"\n⚠️ {msg}")
                    choice = input("(s)kip, (e)dit, (a)ccept? ").strip().lower()
                    if choice == 's': return None
                    if choice == 'e':
                        current_val = input("Enter new value: ").strip()
                        continue
                    # 'a' accepts (proceeds to next check or returns)
                else:
                    logging.info(f"Skipping '{current_val}' (too short)")
                    return None

            # 3. Person Check (Missing Name Parts)
            if category in ['dirigent', 'komponist', 'solist']:
                first, last = self.split_name(current_val)

                if not first or not last:
                    # If authorized (e.g. from Map), we allow single names (first="" or last="")
                    if authorized:
                        return current_val

                    msg = f"Value '{current_val}' is missing First or Last Name."
                    if self.interactive:
                        print(f"\n⚠️ {msg}")
                        choice = input("(s)kip, (e)dit, (o)k? ").strip().lower()
                        if choice == 's': return None
                        if choice == 'e':
                            current_val = input("Enter full correct name: ").strip()
                            continue
                        if choice == 'o': return current_val
                    else:
                        logging.info(f"Skipping '{current_val}' (incomplete name)")
                        return None

            return current_val

    def load_correction_map(self, filepath):
        if not filepath or not os.path.exists(filepath):
            logging.warning(f"⚠️ Korrektur-Map nicht gefunden: {filepath}")
            return
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                self.correction_map = json.load(f)
            logging.info(f"✓ Korrektur-Map geladen ({len(self.correction_map)} Einträge).")
        except Exception as e:
            logging.error(f"❌ Fehler beim Laden der Korrektur-Map: {e}")

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

    def load_existing_data(self):
        """Loads existing entities from the new database into the cache."""
        logging.info("Loading existing data from new database...")

        # Helper to load simple entities
        def load_simple(table, cache_key):
            try:
                self.new_cur.execute(f"SELECT Id, Name FROM {table}")
                for row in self.new_cur.fetchall():
                    norm = self.normalize_name(row[1])
                    if norm:
                        self.cache[cache_key][norm] = row[0]
            except Exception as e:
                logging.error(f"Error loading {table}: {e}")

        # Helper to load person entities (with Vorname)
        def load_person(table, cache_key):
            try:
                self.new_cur.execute(f"SELECT Id, Vorname, Name FROM {table}")
                for row in self.new_cur.fetchall():
                    full_name = f"{row[1]} {row[2]}".strip()
                    norm = self.normalize_name(full_name)
                    if norm:
                        self.cache[cache_key][norm] = row[0]
                        self.update_name_index(cache_key, full_name, row[0])
            except Exception as e:
                logging.error(f"Error loading {table}: {e}")

        load_person("Dirigenten", "dirigent")
        load_person("Komponisten", "komponist")
        load_person("Solisten", "solist")
        load_simple("Orchester", "orchester")
        load_simple("Orte", "ort")

        # Werke need special handling (Name + KomponistId)
        try:
            self.new_cur.execute("SELECT Id, Name, KomponistId FROM Werke")
            for row in self.new_cur.fetchall():
                key = f"{row[1]}_{row[2]}"
                norm = self.normalize_name(key) # Normalizing the key?
                # Original script used: werk_key = f"{ai_w}_{k_id}" -> normalize_name(werk_key)
                # Let's keep consistent: key is passed to get_or_create, which normalizes it.
                # So we should normalize the lookup key.
                # But here the key structure is complex.
                # In original script: get_or_create('werk', f"{name}_{k_id}")
                # normalize_name just lowercases and removes accents.
                # So we should normalize the name part? No, the whole string.
                # Just store it normalized.
                if norm:
                    self.cache['werk'][norm] = row[0]
        except Exception as e:
            logging.error(f"Error loading Werke: {e}")

    def update_name_index(self, category, full_name, db_id):
        if category not in self.name_index: return
        first, last = self.split_name(full_name)
        if first and last:
            norm_last = self.normalize_name(last)
            if norm_last not in self.name_index[category]:
                self.name_index[category][norm_last] = []

            # Store tuple of (normalized_full_name, db_id)
            norm_full = self.normalize_name(full_name)
            # Avoid adding duplicates
            if not any(entry[1] == db_id for entry in self.name_index[category][norm_last]):
                self.name_index[category][norm_last].append((norm_full, db_id))

    def ensure_entity(self, category, value, insert_sql, params):
        """
        Tries to find the entity. If not found, creates it.
        Handles ambiguities via interactive mode or skipping.
        Returns ID or None if skipped.
        """
        if not value: return None

        # 1. Apply Correction Map (Priority 1)
        # If in map, we trust the mapped value ("authorized").
        val_to_use = value
        authorized = False

        if value in self.correction_map:
            val_to_use = self.correction_map[value]
            authorized = True

        while True:
            norm_key = self.normalize_name(val_to_use)

            # 2. Cache Lookup (Priority 2: Complete Entry)
            if norm_key in self.cache[category]:
                return self.cache[category][norm_key]

            # 3. Validation
            # Returns cleaned value or None (if skipped).
            # If user edits here, val_to_use changes.
            validated_val = self.validate_and_fix_input(category, val_to_use, authorized=authorized)
            if not validated_val:
                return None # Skip

            if validated_val != val_to_use:
                val_to_use = validated_val
                authorized = True # User manually edited/accepted it
                continue # Restart loop to check cache/map for new value

            # 4. Name-Only Lookup (for Person categories)
            if category in ['dirigent', 'komponist', 'solist']:
                first, last = self.split_name(val_to_use)
                if not first and last: # Name only provided (e.g. "Beethoven")
                    norm_last = self.normalize_name(last)
                    candidates = self.name_index[category].get(norm_last, [])
                    unique_ids = list({c[1] for c in candidates})

                    if len(unique_ids) == 1:
                        # Unique match found
                        found_id = unique_ids[0]
                        self.cache[category][norm_key] = found_id
                        return found_id

                    elif len(unique_ids) > 1:
                        # Ambiguous
                        if self.interactive:
                            print(f"\n⚠️ Ambiguity for '{val_to_use}' ({category}):")
                            options = []
                            for i, uid in enumerate(unique_ids):
                                table = "Komponisten" if category == "komponist" else "Dirigenten" if category == "dirigent" else "Solisten"
                                self.new_cur.execute(f"SELECT Vorname, Name FROM {table} WHERE Id = %s", (uid,))
                                res = self.new_cur.fetchone()
                                name_display = f"{res[0]} {res[1]}" if res else f"ID {uid}"
                                options.append((uid, name_display))
                                print(f"   {i+1}: {name_display}")

                            print(f"   0: Create new '{val_to_use}'")
                            print(f"   s: Skip")

                            choice = input("Select option: ").strip().lower()
                            if choice == 's':
                                logging.info(f"Skipped ambiguous '{val_to_use}'")
                                return None
                            elif choice == '0':
                                authorized = True # Proceed
                            elif choice.isdigit() and 1 <= int(choice) <= len(options):
                                selected_id = options[int(choice)-1][0]
                                self.cache[category][norm_key] = selected_id
                                return selected_id
                            else:
                                logging.warning("Invalid choice. Creating new.")
                                authorized = True
                        else:
                            logging.warning(f"Ambiguous '{val_to_use}' skipped in non-interactive mode. Candidates: {len(unique_ids)}")
                            return None # Skip

            # 5. Unknown Entity Check (Priority 3: Auto-Authorize Valid Entries or Ask)
            if not authorized:
                # Check for "Valid" entry to auto-authorize
                is_valid_auto = False

                if category in ['dirigent', 'komponist', 'solist']:
                    # Person: Needs First + Last Name
                    f, l = self.split_name(val_to_use)
                    if f and l:
                        is_valid_auto = True
                        logging.info(f"Auto-accepting valid person: '{val_to_use}'")

                elif category in ['orchester', 'ort']:
                    # Orchestra/Ort: Just needs to be valid string (already passed validation above)
                    # If it passed validation step 3, it's non-empty and > 3 chars
                    is_valid_auto = True
                    logging.info(f"Auto-accepting {category}: '{val_to_use}'")

                if is_valid_auto:
                    authorized = True

                # If still not authorized, ask interactively
                elif self.interactive:
                    print(f"\n❓ Entity '{val_to_use}' ({category}) not found in DB or Map.")
                    choice = input("(c)reate, (s)kip, (e)dit? ").strip().lower()

                    if choice == 's':
                        logging.info(f"Skipped unknown '{val_to_use}'")
                        return None
                    elif choice == 'e':
                        new_val = input("Enter new value: ").strip()
                        if new_val:
                            val_to_use = new_val
                            authorized = True
                            continue # Restart loop
                        else:
                            return None
                    elif choice == 'c':
                         pass # Proceed
                    else:
                         pass # Default
                else:
                    # Non-interactive: Proceed (or skip? Defaulting to create/proceed as per original behavior)
                    pass

            # 6. Insert
            try:
                final_params = params
                if val_to_use != value:
                    if category in ['dirigent', 'solist', 'komponist']:
                        parts = self.split_name(val_to_use)
                        if category == 'komponist':
                            final_params = (*parts, "")
                        else:
                            final_params = parts
                    elif category in ['orchester', 'ort']:
                        final_params = (val_to_use,)

                self.new_cur.execute(insert_sql, final_params)
                new_id = self.new_cur.lastrowid
                self.new_conn.commit()

                self.cache[category][norm_key] = new_id
                self.update_name_index(category, val_to_use, new_id)
                logging.info(f"Created {category}: {val_to_use}")
                return new_id
            except Error as e:
                logging.error(f"Failed to insert {category} '{val_to_use}': {e}")
                return None

            # Break the loop if we reached here
            break

    def lookup_entity(self, category, value):
        """
        Strict lookup. Returns ID if found (unique), None otherwise.
        """
        if not value: return None

        # 1. Apply Correction
        val_to_use = self.correction_map.get(value, value)
        norm_key = self.normalize_name(val_to_use)

        # 2. Cache Lookup
        if norm_key in self.cache[category]:
            return self.cache[category][norm_key]

        # 3. Name-Only Lookup
        if category in ['dirigent', 'komponist', 'solist']:
            first, last = self.split_name(val_to_use)
            if not first and last:
                norm_last = self.normalize_name(last)
                candidates = self.name_index[category].get(norm_last, [])
                unique_ids = list({c[1] for c in candidates})

                if len(unique_ids) == 1:
                    return unique_ids[0]

                # If ambiguous or 0, return None
                if len(unique_ids) > 1:
                    logging.warning(f"Lookup ambiguous for '{val_to_use}'")
                return None

        return None

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

    def migrate_works(self):
        logging.info("Starting Work migration...")
        # Works are tricky because they depend on Composer.
        # We need (Werk, Komponist) pairs from Old DB.
        self.old_cur.execute("SELECT DISTINCT Werk, Komponist FROM MusicRecords WHERE Werk IS NOT NULL AND Werk != ''")
        count = 0
        skipped = 0

        for row in self.old_cur.fetchall():
            werk_name = row['Werk']
            komp_str = row['Komponist']

            # We need to resolve the composer(s).
            # If multiple composers, we might have multiple works or "Todo" logic?
            # Original script logic:
            # If standard case (1 composer): Create Werk linked to Composer.
            # If multiple composers: Create "Todo" works?
            # User requirement: "check all entries in werke and add those who are not found"
            # But "Werke" in old DB is just a string.
            # In New DB, Work = (Name, ComposerId).

            # So we must try to match the Composer string to an existing Composer ID.

            resolved_composers = self.parse_complex_entity_string(komp_str, 'komponist')

            if not resolved_composers:
                logging.warning(f"Work '{werk_name}' has no composer. Skipped.")
                skipped += 1
                continue

            # Simplification: Only handle the FIRST resolved composer for the named work.
            # (Because we don't know which work belongs to which composer if there are multiple).

            first_komp_str = resolved_composers[0]
            k_id = self.lookup_entity('komponist', first_komp_str)

            if not k_id:
                logging.error(f"Work '{werk_name}': Composer '{first_komp_str}' not found. Skipped.")
                skipped += 1
                continue

            # Composer found. Ensure Work exists.
            # Key for cache/lookup: "Name_ComposerId"
            werk_key = f"{werk_name}_{k_id}"

            # Check if exists (ensure_entity does this via cache, but cache key needs to be set up right)
            # In ensure_entity:
            # norm_key = self.normalize_name(value)
            # We should pass the composite key as value? No, value is used for insert params usually.
            # But 'werk' is special.
            # ensure_entity takes (category, value, sql, params).
            # For Werk:
            # value = werk_key (so caching works on the composite)
            # params = (werk_name, k_id)

            if self.ensure_entity('werk', werk_key,
                "INSERT INTO Werke (Name, KomponistId) VALUES (%s, %s)",
                (werk_name, k_id)):
                count += 1

        logging.info(f"Work migration finished. Processed {count}, Skipped {skipped}.")

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
                komp_parts = self.split_komponist_string(row['Komponist'])
                if not komp_parts:
                     # If Werk exists but no Komponist? Rare but possible.
                     # In migrate_works, we skipped.
                     missing_deps.append(f"Werk (No Composer): {row['Werk']}")
                else:
                    # Try to find the work using the FIRST composer (consistent with migrate_works)
                    first_komp_str = komp_parts[0]
                    k_id = self.lookup_entity('komponist', first_komp_str)

                    if not k_id:
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
                    # Handle tuple vs dict cursor. Standard cursor is tuple unless dictionary=True.
                    # We initialized self.new_cur as self.new_conn.cursor() which is tuple.
                    # Indexes: 0:Id, 1:Spielsaison, 2:Bewertung, 3:OrtId, 4:DirigentId, 5:OrchesterId
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
    parser.add_argument('--map', type=str, help="Path to correction map JSON")

    args = parser.parse_args()
    
    spec = MigrationSpecialist(interactive=args.interactive)

    if args.map:
        spec.load_correction_map(args.map)

    if args.delete:
        spec.clear_new_db()

    if args.delete_doc:
        spec.clear_documents()

    if args.delete_note:
        spec.clear_notes()

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
