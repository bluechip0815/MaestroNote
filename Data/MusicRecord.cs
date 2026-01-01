using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class MusicRecord
    {
        [Key]
        public int Id { get; set; } = 0;
        [MaxLength(64)]
        public string Komponist { get; set; } = "";
        [MaxLength(100)]
        public string Werk { get; set; } = "";
        [MaxLength(128)]
        public string Orchester { get; set; } = "";
        [MaxLength(64)]
        public string Dirigent { get; set; } = "";
        [MaxLength(256)]
        public string Solist { get; set; } = "";
        public DateTime Datum { get; set; } = DateTime.Now;
        [MaxLength(64)]
        public string Spielsaison { get; set; } = $"{DateTime.Now.Year}/{(DateTime.Now.Year+1)%100}";
        [MaxLength(1000)]
        public string Bewertung1 { get; set; } = "";
        [MaxLength(1000)]
        public string Bewertung2 { get; set; } = "";
        [MaxLength(64)]
        public string Ort { get; set; } = "";
        public void Init(MusicRecord n)
        {
            Id = n.Id;
            Komponist = n.Komponist;
            Werk = n.Werk;
            Orchester = n.Orchester;
            Dirigent = n.Dirigent;
            Solist = n.Solist;
            Datum = n.Datum;
            Bewertung1 = n.Bewertung1;
            Bewertung2 = n.Bewertung2;
            Ort = n.Ort;
            Spielsaison = n.Spielsaison;   
        }
        public bool Find(string filter)
        {
            if (Komponist.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Werk.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Orchester.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Dirigent.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Solist.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Spielsaison.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Bewertung1.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Bewertung2.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (Ort.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }
    }
}
