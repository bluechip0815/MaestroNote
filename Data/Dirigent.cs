using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class Dirigent
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(50)]
        public string Vorname { get; set; } = "";
        [MaxLength(50)]
        public string Name { get; set; } = "";
        public DateTime? Born { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }

        public DateTime? Died { get; set; }
        // ALTER TABLE Dirigenten ADD Died DATETIME NULL;
    }
}
