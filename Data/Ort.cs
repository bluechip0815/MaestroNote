using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class Ort
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(2000)]
        public string? Note { get; set; }
    }
}
