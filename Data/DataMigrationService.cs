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
            {
                // Check if we need to migrate Orte
                MigrateOrte(context);
                return; // Already migrated
            }

            context.SaveChanges();
        }

        private static void MigrateOrte(MusicContext context)
        {
            try
            {
                // Find records with string Ort but no OrtId
                var recordsToMigrate = context.MusicRecords
                    .Where(m => !string.IsNullOrEmpty(m.Ort) && m.OrtId == null)
                    .ToList();

                if (!recordsToMigrate.Any())
                    return;

                var existingOrte = context.Orte.ToList();
                bool changes = false;

                foreach (var record in recordsToMigrate)
                {
                    string ortName = record.Ort.Trim();
                    if (string.IsNullOrEmpty(ortName)) continue;

                    var ortEntity = existingOrte.FirstOrDefault(o => o.Name.Equals(ortName, StringComparison.OrdinalIgnoreCase));
                    if (ortEntity == null)
                    {
                        ortEntity = new Ort { Name = ortName };
                        context.Orte.Add(ortEntity);
                        existingOrte.Add(ortEntity); // Add to local cache
                        changes = true;
                    }

                    record.OrtEntity = ortEntity;
                    // record.OrtId will be set by EF upon save or we can set it if ID is known
                    if (ortEntity.Id > 0) record.OrtId = ortEntity.Id;

                    changes = true;
                }

                if (changes)
                {
                    context.SaveChanges();
                }
            }
            catch (Exception)
            {
                // Log error?
            }
        }
    }
}
