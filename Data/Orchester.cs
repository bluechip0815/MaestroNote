using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class Orchester
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(100)]
        public string Name { get; set; } = "";
        public DateTime? Founded { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }

    }
}
