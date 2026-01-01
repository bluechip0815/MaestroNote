using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaestroNotes.Data
{
    public class MusicRecord
    {
        [Key]
        public int Id { get; set; } = 0;

        // --- Legacy Fields ---
        [MaxLength(64)]
        [Column("Komponist")]
        public string KomponistLegacy { get; set; } = "";

        [MaxLength(100)]
        [Column("Werk")]
        public string WerkLegacy { get; set; } = "";

        [MaxLength(128)]
        [Column("Orchester")]
        public string OrchesterLegacy { get; set; } = "";

        [MaxLength(64)]
        [Column("Dirigent")]
        public string DirigentLegacy { get; set; } = "";

        [MaxLength(256)]
        [Column("Solist")]
        public string SolistLegacy { get; set; } = "";

        [MaxLength(1000)]
        [Column("Bewertung1")]
        public string Bewertung1Legacy { get; set; } = "";

        [MaxLength(1000)]
        [Column("Bewertung2")]
        public string Bewertung2Legacy { get; set; } = "";
        // ---------------------

        [MaxLength(200)]
        public string Bezeichnung { get; set; } = "";

        public DateTime Datum { get; set; } = DateTime.Now;
        [MaxLength(64)]
        public string Spielsaison { get; set; } = $"{DateTime.Now.Year}/{(DateTime.Now.Year+1)%100}";

        [MaxLength(2000)]
        public string Bewertung { get; set; } = "";

        [MaxLength(64)]
        public string Ort { get; set; } = "";

        // Relationships
        public int? DirigentId { get; set; }
        [ForeignKey("DirigentId")]
        public Dirigent? Dirigent { get; set; }

        public int? OrchesterId { get; set; }
        [ForeignKey("OrchesterId")]
        public Orchester? Orchester { get; set; }

        public List<Werk> Werke { get; set; } = new();

        public List<Solist> Solisten { get; set; } = new();


        public void Init(MusicRecord n)
        {
            Id = n.Id;
            // Map legacy to new for init logic if needed, but primarily use new fields
            Bezeichnung = n.Bezeichnung;

            Dirigent = n.Dirigent;
            DirigentId = n.DirigentId;

            Orchester = n.Orchester;
            OrchesterId = n.OrchesterId;

            Werke = n.Werke;
            Solisten = n.Solisten;

            Datum = n.Datum;
            Bewertung = n.Bewertung;
            Ort = n.Ort;
            Spielsaison = n.Spielsaison;   

            // Legacy init for safety
            KomponistLegacy = n.KomponistLegacy;
            WerkLegacy = n.WerkLegacy;
            OrchesterLegacy = n.OrchesterLegacy;
            DirigentLegacy = n.DirigentLegacy;
            SolistLegacy = n.SolistLegacy;
            Bewertung1Legacy = n.Bewertung1Legacy;
            Bewertung2Legacy = n.Bewertung2Legacy;
        }

        public bool Find(string filter)
        {
            // Search in new fields
            if (Bezeichnung.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Ort.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Spielsaison.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Bewertung.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;

            // Search in linked entities
            if (Dirigent?.Name?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true) return true;
            if (Orchester?.Name?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true) return true;
            if (Werke.Any(w => w.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                            || (w.Komponist?.Name?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true))) return true;
            if (Solisten.Any(s => s.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))) return true;

            // Fallback to legacy search if migration hasn't happened or to be safe
            if (KomponistLegacy.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (WerkLegacy.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (OrchesterLegacy.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (DirigentLegacy.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (SolistLegacy.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;

            return false;
        }
    }
}
