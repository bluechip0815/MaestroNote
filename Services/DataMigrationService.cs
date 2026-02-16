using MaestroNotes.Data;
using Microsoft.EntityFrameworkCore;

namespace MaestroNotes.Services
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
                // Orte migration is now handled in DB migration script
                return; // Already migrated
            }

            context.SaveChanges();
        }
    }
}
