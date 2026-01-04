using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaestroNotes.Data
{
    public class Werk
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(200)]
        public string Name { get; set; } = "";
        [MaxLength(1000)]
        public string? Note { get; set; }

        public int? KomponistId { get; set; }
        [ForeignKey("KomponistId")]
        public Komponist? Komponist { get; set; }

        public List<MusicRecord> MusicRecords { get; set; } = new();
    }
}
