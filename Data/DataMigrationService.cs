using Microsoft.EntityFrameworkCore;

namespace MaestroNotes.Data
{
    public class DataMigrationService
    {
        public static void MigrateData(MusicContext context)
        {
            // Ensure database is created (this might be handled elsewhere, but good to ensure)
            // Note: If using migrations, this might need to be skipped or handled carefully.
            // context.Database.EnsureCreated();

            // Check if we need to migrate: if new tables are empty but legacy data exists.
            if (!context.MusicRecords.Any())
                return; // No data at all

            // If Werke table is empty, we assume migration is needed.
            if (context.Werke.Any())
                return; // Already migrated

            var records = context.MusicRecords
                .Include(m => m.Werke)
                .Include(m => m.Solisten)
                .ToList();

            foreach (var record in records)
            {
                // 1. Orchester
                if (!string.IsNullOrWhiteSpace(record.OrchesterLegacy))
                {
                    var orchesterName = record.OrchesterLegacy.Trim();
                    var orchester = context.Orchester.Local.FirstOrDefault(o => o.Name == orchesterName)
                                    ?? context.Orchester.FirstOrDefault(o => o.Name == orchesterName);

                    if (orchester == null)
                    {
                        orchester = new Orchester { Name = orchesterName };
                        context.Orchester.Add(orchester);
                    }
                    record.Orchester = orchester;
                }

                // 2. Dirigent
                if (!string.IsNullOrWhiteSpace(record.DirigentLegacy))
                {
                    var dirigentName = record.DirigentLegacy.Trim();
                    var dirigent = context.Dirigenten.Local.FirstOrDefault(d => d.Name == dirigentName)
                                   ?? context.Dirigenten.FirstOrDefault(d => d.Name == dirigentName);

                    if (dirigent == null)
                    {
                        dirigent = new Dirigent { Name = dirigentName };
                        context.Dirigenten.Add(dirigent);
                    }
                    record.Dirigent = dirigent;
                }

                // 3. Solist (Treat as single entry per requirements)
                if (!string.IsNullOrWhiteSpace(record.SolistLegacy))
                {
                    var solistName = record.SolistLegacy.Trim();
                    var solist = context.Solisten.Local.FirstOrDefault(s => s.Name == solistName)
                                 ?? context.Solisten.FirstOrDefault(s => s.Name == solistName);

                    if (solist == null)
                    {
                        solist = new Solist { Name = solistName };
                        context.Solisten.Add(solist);
                    }
                    if (!record.Solisten.Contains(solist))
                    {
                        record.Solisten.Add(solist);
                    }
                }

                // 4. Komponist & Werk
                // We need both to create a unique Werk entry (Name + Komponist)
                if (!string.IsNullOrWhiteSpace(record.WerkLegacy))
                {
                    var werkName = record.WerkLegacy.Trim();
                    var komponistName = record.KomponistLegacy?.Trim() ?? "";

                    Komponist? komponist = null;
                    if (!string.IsNullOrEmpty(komponistName))
                    {
                        komponist = context.Komponisten.Local.FirstOrDefault(k => k.Name == komponistName)
                                    ?? context.Komponisten.FirstOrDefault(k => k.Name == komponistName);
                        if (komponist == null)
                        {
                            komponist = new Komponist { Name = komponistName };
                            context.Komponisten.Add(komponist);
                        }
                    }

                    // Find or Create Werk
                    // Note: We search by Name AND Komponist (if exists)
                    var werk = context.Werke.Local.FirstOrDefault(w => w.Name == werkName && (komponist == null || w.Komponist == komponist))
                               ?? context.Werke.FirstOrDefault(w => w.Name == werkName && (komponist == null || w.KomponistId == komponist.Id));

                    if (werk == null)
                    {
                        werk = new Werk { Name = werkName, Komponist = komponist };
                        context.Werke.Add(werk);
                    }

                    if (!record.Werke.Contains(werk))
                    {
                        record.Werke.Add(werk);
                    }

                    // Set Bezeichnung to first Werk name as requested
                    if (string.IsNullOrEmpty(record.Bezeichnung))
                    {
                        record.Bezeichnung = werkName;
                    }
                }

                // 5. Bewertung Merge
                var b1 = record.Bewertung1Legacy?.Trim();
                var b2 = record.Bewertung2Legacy?.Trim();
                var combined = "";
                if (!string.IsNullOrEmpty(b1)) combined += b1;
                if (!string.IsNullOrEmpty(b1) && !string.IsNullOrEmpty(b2)) combined += "\n";
                if (!string.IsNullOrEmpty(b2)) combined += b2;

                record.Bewertung = combined;
            }

            context.SaveChanges();
        }
    }
}
