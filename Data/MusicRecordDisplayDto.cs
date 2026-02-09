using static MaestroNotes.Data.MusicService;

namespace MaestroNotes.Data
{
    public class MusicRecordDisplayDto
    {
        public int Id { get; set; }
        public DateTime Datum { get; set; }
        public string Spielsaison { get; set; } = "";
        public string Ort { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public string Bewertung { get; set; } = "";

        public string KomponistNames { get; set; } = "";
        public string WerkNames { get; set; } = "";
        public string OrchesterName { get; set; } = "";
        public string DirigentName { get; set; } = "";
        public string SolistNames { get; set; } = "";

        // Image Management
        public bool HasImages { get; set; } = false;
        public bool ShowImages { get; set; } = false;
        public bool ImagesLoaded { get; set; } = false;
        public List<DOC> Images { get; set; } = new();
    }
}
