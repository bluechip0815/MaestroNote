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
    }
}
