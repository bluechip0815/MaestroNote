using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class Solist
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(100)]
        public string Name { get; set; } = "";
        public DateTime? Born { get; set; }
        [MaxLength(1000)]
        public string Note { get; set; } = "";
        public List<MusicRecord> MusicRecords { get; set; } = new();
    }
}
