namespace MaestroNotes.Data
{
    // Define the RtfRecord class structure
    public class RtfRecord
    {
        public RtfPerson _komponist { get; set; } = new();
        public string Werk { get; set; } = "";
        public string Orchester { get; set; } = "";
        public RtfPerson _dirigent { get; set; } = new();
        public string Solist { get; set; } = "";
        public string Datum { get; set; } = "";
        public string Spielsaison { get; set; } = "";
        public string Bewertung { get; set; } = "";
        public string Ort { get; set; } = "";
        public string Komponist => _komponist.Name;
        public string Dirigent => _dirigent.Name;
        public void Update()
        {
            _dirigent.Update();
            _komponist.Update();
        }
    }
}