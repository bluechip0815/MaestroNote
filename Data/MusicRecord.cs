using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaestroNotes.Data
{
    public class MusicRecord
    {
        [Key]
        public int Id { get; set; } = 0;

        [MaxLength(200)]
        public string Bezeichnung { get; set; } = "";

        public DateTime Datum { get; set; } = DateTime.Now;
        [MaxLength(64)]
        public string Spielsaison { get; set; } = $"{DateTime.Now.Year}/{(DateTime.Now.Year+1)%100}";

        [MaxLength(2000)]
        public string Bewertung { get; set; } = "";

        // Relationships
        public int? DirigentId { get; set; }
        [ForeignKey("DirigentId")]
        public Dirigent? Dirigent { get; set; }

        public int? OrchesterId { get; set; }
        [ForeignKey("OrchesterId")]
        public Orchester? Orchester { get; set; }

        public int? OrtId { get; set; }
        [ForeignKey("OrtId")]
        public Ort? OrtEntity { get; set; }

        public List<Werk> Werke { get; set; } = new();

        public List<Solist> Solisten { get; set; } = new();


        public void Init(MusicRecord n)
        {
            Id = n.Id;
            Bezeichnung = n.Bezeichnung;

            Dirigent = n.Dirigent;
            DirigentId = n.DirigentId;

            Orchester = n.Orchester;
            OrchesterId = n.OrchesterId;

            OrtEntity = n.OrtEntity;
            OrtId = n.OrtId;

            Werke = n.Werke;
            Solisten = n.Solisten;

            Datum = n.Datum;
            Bewertung = n.Bewertung;
            Spielsaison = n.Spielsaison;
        }
    }
}
