using Microsoft.EntityFrameworkCore;

namespace MaestroNotes.Data
{
    public class MusicContext : DbContext
    {
        public DbSet<MusicRecord> MusicRecords { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Komponist> Komponisten { get; set; }
        public DbSet<Dirigent> Dirigenten { get; set; }
        public DbSet<Solist> Solisten { get; set; }
        public DbSet<Orchester> Orchester { get; set; }
        public DbSet<Werk> Werke { get; set; }

        public string DocumentsPath { get; set; } = "";
        public string ImagesPath { get; set; } = "";
        public string PasswordRO { get; set; } = "";
        public string PasswordRW { get; set; } = "";
        public MusicContext(DbContextOptions<MusicContext> options, IConfiguration cfg) : base(options)
        {
            DocumentsPath = cfg.GetValue<string>("Documents") ?? "";
            ImagesPath = cfg.GetValue<string>("Images") ?? "";
            PasswordRO = cfg.GetValue<string>("Password") ?? "Gast";
            PasswordRW = cfg.GetValue<string>("Password-Write") ?? "Nilsau";
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

    }
}
