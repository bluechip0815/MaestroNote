namespace MaestroNotes.Data
{
    public static class DefaultLists
    {
        static public List<string> SpielSaisonList { get; private set; } = new();
        static public List<string> KomponistList { get; private set; } = new();
        static public List<string> WerkList { get; private set; } = new();
        static public List<string> OrchesterList { get; private set; } = new();
        static public List<string> DirigentList { get; private set; } = new();
        static public List<string> SolistList { get; private set; } = new();
        static public List<string> OrtList { get; private set; } = new();
        public static void Init(MusicContext _context)
        {
            if (_context is null)
                return;

            string n = $"{DateTime.UtcNow.Year}/{(DateTime.UtcNow.Year + 1) % 100}";
            SpielSaisonList = _context.MusicRecords
                           .Select(m => m.Spielsaison)
                           .Distinct()
                           .ToList();
            if (!SpielSaisonList.Contains(n))
                SpielSaisonList.Add(n);

            KomponistList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Komponist))
                           .Select(m => m.Komponist)
                           .Distinct()
                           .ToList();

            WerkList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Werk))
                           .Select(m => m.Werk)
                           .Distinct()
                           .ToList();

            OrchesterList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Orchester))
                           .Select(m => m.Orchester)
                           .Distinct()
                           .ToList();

            DirigentList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Dirigent))
                           .Select(m => m.Dirigent)
                           .Distinct()
                           .ToList();

            SolistList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Solist))
                           .Select(m => m.Solist)
                           .Distinct()
                           .ToList();

            OrtList = _context.MusicRecords
                           .Where(m => !string.IsNullOrEmpty(m.Ort))
                           .Select(m => m.Ort)
                           .Distinct()
                           .ToList();
        }
    }
}
