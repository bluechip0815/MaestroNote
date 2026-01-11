import mysql.connector
from mysql.connector import Error
import argparse
import sys
import os

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
    def __init__(self):
        try:
            self.old_conn = mysql.connector.connect(**DB_CONFIG["old"])
            self.new_conn = mysql.connector.connect(**DB_CONFIG["new"])
            self.old_cur = self.old_conn.cursor(dictionary=True)
            self.new_cur = self.new_conn.cursor()
            print("✓ Datenbanken verbunden.")
        except Error as e:
            print(f"❌ Verbindungsfehler: {e}")
            sys.exit(1)

        # Caching um Duplikate in den Stammdaten zu vermeiden
        self.cache = {'dirigent': {}, 'orchester': {}, 'komponist': {}, 'werk': {}, 'solist': {}, 'ort': {}}

    def split_name(self, full_name):
        if not full_name: return "", ""
        parts = full_name.strip().split(' ')
        if len(parts) == 1: return "", parts[0]
        return " ".join(parts[:-1]), parts[-1]

    def clear_new_db(self):
        print("⚠️ Lösche neue Datenbank für frischen Import...")
        # EF Core Standard-Tabellennamen für n:m Beziehungen
        tables = [
            "MusicRecordSolist", "MusicRecordWerk", "MusicRecords", 
            "Solisten", "Werke", "Komponisten", "Orchester", "Dirigenten", "Orte"
        ]
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 0;")
        for table in tables:
            try: self.new_cur.execute(f"TRUNCATE TABLE {table};")
            except: pass
        self.new_cur.execute("SET FOREIGN_KEY_CHECKS = 1;")
        self.new_conn.commit()

    def get_or_create(self, cache_key, lookup_val, insert_sql, params):
        if lookup_val in self.cache[cache_key]:
            return self.cache[cache_key][lookup_val]
        self.new_cur.execute(insert_sql, params)
        new_id = self.new_cur.lastrowid
        self.cache[cache_key][lookup_val] = new_id
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
                
                werk_key = f"{row['Werk']}_{k_id}"
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
                for s_full in [s.strip() for s in row['Solist'].split(',')]:
                    s_id = self.get_or_create('solist', s_full, 
                        "INSERT INTO Solisten (Vorname, Name) VALUES (%s, %s)", self.split_name(s_full))
                    
                    self.new_cur.execute(
                        "INSERT INTO MusicRecordSolist (MusicRecordsId, SolistenId) VALUES (%s, %s)", 
                        (row['Id'], s_id)
                    )

        self.new_conn.commit()
        print(f"✅ Fertig! {len(old_data)} Records migriert.")

    def close(self):
        self.old_cur.close(); self.new_cur.close()
        self.old_conn.close(); self.new_conn.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--delete', action='store_true')
    parser.add_argument('--transfer', action='store_true')
    args = parser.parse_args()
    
    spec = MigrationSpecialist()
    if args.delete: spec.clear_new_db()
    if args.transfer: spec.transfer()
    spec.close()