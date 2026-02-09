namespace MaestroNotes.Data.Export
{
    // Define the MusicRecord class structure
    public class RtfRecord
    {
        public string Werk { get; set; } = "";
        public Person Komponist { get; set; } = new();
        public Person Dirigent { get; set; } = new();
        public string Orchester { get; set; } = "";
        public List<Person> Solist { get; set; } = new();
        public string Datum { get; set; } = "";
        public string Spielsaison { get; set; } = "";
        public string Bewertung { get; set; } = "";
        public string Ort { get; set; } = "";
    }
    public class Person 
    {
        public string Name { get; set; } = "";
        public string Vorname { get; set; } = "";
    }
}