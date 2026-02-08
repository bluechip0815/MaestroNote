using System.Text;

namespace DbToRtf
{
    public class RtfExporter
    {
        int LeftTab = 0;
        int UsableTwips = 0;
        int FsValue16 = 0;   // because you’re using \fs16
        int LineSpacing = 0; // ~192 twips

        public string ImagePath { get; private set; } = "";
        public RtfExporter(string p)
        {
            LeftTab = CmToTwips(0.5);
            UsableTwips = CmToTwips(8.25);
            FsValue16 = 16;   // because you’re using \fs16
            LineSpacing = LineSpacingTwips(FsValue16, 1.8); // ~192 twips

            ImagePath = p;
            if (!ImagePath.EndsWith(System.IO.Path.DirectorySeparatorChar))
                ImagePath += System.IO.Path.DirectorySeparatorChar;
        }
        public void ExportToRtf(string rtfFilePath, List<MusicRecord> records)
        {
            // --- Top 10 Komponisten ---
            var topKomponisten = records
                .Where(r => !string.IsNullOrWhiteSpace(r.Komponist))
                .GroupBy(r => r.Komponist.Trim())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            // --- Top 10 Dirigenten ---
            var topDirigenten = records
                .Where(r => !string.IsNullOrWhiteSpace(r.Dirigent))
                .GroupBy(r => r.Dirigent.Trim())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            var sb = new StringBuilder();
            AppendFrontMatter(sb);
            ExportKomponistenToRtf(records, sb, topKomponisten);
            sb.AppendLine(@"\page");
            ExportDirigentenToRtf(records, sb, topDirigenten);
            sb.AppendLine(@"\page");
            ExportImpressionenToRtf(records, sb);

            sb.AppendLine(@"\page");
            ExportTop10ToRtf(records, sb, topKomponisten, topDirigenten);
            sb.AppendLine(@"\page");

            sb.AppendLine(@"}");

            File.WriteAllText(rtfFilePath, sb.ToString(), Encoding.Default);
            Console.WriteLine("RTF file written: " + rtfFilePath);
        }

        private void ExportTop10ToRtf(List<MusicRecord> records, StringBuilder sb, List<IGrouping<string, MusicRecord>> topKomponisten, List<IGrouping<string, MusicRecord>> topDirigenten)
        {
            sb.AppendLine(@"\pard\qc\f0\fs24\b Top 10\b0\par\par");

            // --- Top 10 Komponisten ---
            sb.AppendLine(@"\pard\ql\f0\fs20\b Komponisten\b0\par");

            foreach (var g in topKomponisten)
            {
                sb.AppendLine($@"\pard\ql\f0\fs16 {EscapeRtf(g.Key)} ({g.Count()})\par");
                sb.AppendLine(@"\par"); // ← extra line
            }

            sb.AppendLine(@"\par");

            // --- Top 10 Dirigenten ---
            sb.AppendLine(@"\pard\ql\f0\fs20\b Dirigenten\b0\par");

            foreach (var g in topDirigenten)
            {
                sb.AppendLine($@"\pard\ql\f0\fs16 {EscapeRtf(g.Key)} ({g.Count()})\par");
                sb.AppendLine(@"\par");
            }

            sb.AppendLine(@"\par");
        }

        private void ExportKomponistenToRtf(List<MusicRecord> records, StringBuilder sb, List<IGrouping<string, MusicRecord>> topKomponisten)
        {
            var sorted = records
                .OrderBy(r => r._komponist.OrderName)
                .ThenBy(r => r.Werk)
                .ThenBy(r => r.Dirigent)
                .ThenBy(r => DateTime.TryParse(r.Datum, out var dt) ? dt : DateTime.MinValue)
                .ToList();

            List<string> list = ["",""];
            double w = (9 * 1440) / 2.54;
            double h = w * 2 / 3;
            AppendImageToRtf(sb, "Komponisten.jpg", "Komponisten", list, w, h, false);

            string? lastKomponist = null;
            foreach (var record in sorted)
            {
                if (string.IsNullOrWhiteSpace(record.Komponist) ||
                    string.IsNullOrWhiteSpace(record.Werk))
                    continue;

                if (record.Komponist.Contains("div.", StringComparison.CurrentCultureIgnoreCase) ||
                    record.Werk.Contains("div.", StringComparison.CurrentCultureIgnoreCase) ||
                    record.Werk.Equals("-", StringComparison.CurrentCultureIgnoreCase) ||
                    record.Werk.Contains("divers", StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (record.Komponist != lastKomponist)
                {
                    if (lastKomponist != null)
                        sb.AppendLine(@"\par");

                    // Method 1: Using Any()
                    if (topKomponisten.Any(g => g.Key.Equals(record.Komponist, StringComparison.OrdinalIgnoreCase)))
                    {
                        // insert a picture
                        AppendImageToRtf(sb, record.Komponist + ".jpg", record.Komponist, null, (7 * 1440) / 2.54);
                    }

                    // Komponist paragraph - bold, left-aligned
                    sb.AppendLine($@"\pard\ql\f0\fs20\b {EscapeRtf(record._komponist.OrderName)}\b0\sa100\par");

                    lastKomponist = record.Komponist;
                }

                // Details line: Werk (left), Dirigent (left), Date (right-aligned)
                sb.AppendLine($@"\pard\ql\li256\fi0\f0\fs16 {EscapeRtf(record.Werk)}, {EscapeRtf(record.Dirigent)}\par");

                // Line 2: Date fully right-aligned (no tab)
                sb.AppendLine($@"\pard\qr\f0\fs16 {EscapeRtf(ShortDate(record.Datum))}\par\par");
            }
        }
        private void ExportDirigentenToRtf(List<MusicRecord> records, StringBuilder sb, List<IGrouping<string, MusicRecord>> topDirigenten)
        {
            var sorted = records
                .OrderBy(r => r._dirigent.OrderName)
                .ThenBy(r => r.Komponist)
                .ThenBy(r => r.Werk)
                .ThenBy(r => DateTime.TryParse(r.Datum, out var dt) ? dt : DateTime.MinValue)
                .ToList();

            List<string> list = ["", ""];
            double w = (9 * 1440) / 2.54;
            double h = w * 2 / 3;
            AppendImageToRtf(sb, "Dirigenten.jpg", "Dirigenten", list, w, h, false);


            string? lastDirigent = null;
            foreach (var record in sorted)
            {
                if (string.IsNullOrWhiteSpace(record.Komponist) ||
                    string.IsNullOrWhiteSpace(record.Dirigent) ||
                    string.IsNullOrWhiteSpace(record.Werk))
                    continue;

                if (record.Komponist.Contains("div.", StringComparison.CurrentCultureIgnoreCase) ||
                   record.Werk.Contains("div.", StringComparison.CurrentCultureIgnoreCase) ||
                   record.Werk.Contains("divers", StringComparison.CurrentCultureIgnoreCase) ||
                   record.Dirigent.Equals("-", StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (record.Dirigent != lastDirigent)
                {
                    lastDirigent = record.Dirigent;

                    if (topDirigenten.Any(g => g.Key.Equals(record.Dirigent, StringComparison.OrdinalIgnoreCase)))
                    {
                        // insert a picture
                        AppendImageToRtf(sb, record.Dirigent + ".jpg", record.Dirigent, null, (9 * 1440) / 2.54);
                    }
                    else
                    {
                        if (lastDirigent != null)
                            sb.AppendLine(@"\par");
                    }
                    sb.AppendLine($@"\pard\ql\f0\fs20\b {EscapeRtf(record._dirigent.OrderName)}\b0\sa100\par");
                }

                // Details line: Werk (left), Dirigent (left), Date (right-aligned)
                sb.AppendLine($@"\pard\ql\li256\fi0\f0\fs16 {EscapeRtf(record.Werk)}, {EscapeRtf(record.Komponist)}\par");

                // Line 2: Date fully right-aligned (no tab)
                sb.AppendLine($@"\pard\qr\f0\fs16 {EscapeRtf(ShortDate(record.Datum))}\par\par");
            }
        }
        private void ExportImpressionenToRtf(List<MusicRecord> records, StringBuilder sb)
        {
            var sorted = records
                .Where(r => !string.IsNullOrWhiteSpace(r.Datum))
                .OrderBy(r => DateTime.TryParse(r.Datum, out var dt) ? dt : DateTime.MinValue)
                .ToList();

            sb.AppendLine(@"\pard\qc\f0\fs24\b Impressionen\b0\par\par");

            string? lastSpielSaison = null;

            foreach (var r in sorted)
            {
                if (string.IsNullOrWhiteSpace(r.Werk) && string.IsNullOrWhiteSpace(r.Bewertung1))
                    continue;

                if (lastSpielSaison != r.Spielsaison && !string.IsNullOrEmpty(r.Spielsaison))
                {
                    sb.AppendLine($@"\pard\ql\f0\fs20\b {r.Spielsaison}\b0\par\par");
                }
                lastSpielSaison = r.Spielsaison;

                string dateStr = ShortDate(r.Datum);

                // Line: Ort, Orchester (left) ⎯⎯⎯ Date (right)
                sb.AppendLine(
                    $@"\pard" +                // start fresh paragraph
                      $@"\li0" +                // indent all text 0cm
                      $@"\sl{LineSpacing}\slmult1" +
                      $@"\tx{UsableTwips}\tqr" +                // set a right-aligned tab at the right edge
                      @"\f0\fs16 " +                // font Verdana, 8pt
                      $"{EscapeRtf(r.Ort)}, {EscapeRtf(r.Orchester)}" +
                      @"\tab " +                // jump to that right tab
                      $"{EscapeRtf(dateStr)}" +
                    @"\sa50\par");


                // Line 2: Werk, Komponist
                if (!string.IsNullOrEmpty(r.Werk) && !string.IsNullOrEmpty(r.Komponist))
                    sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16\b {EscapeRtf(r.Werk)}, {EscapeRtf(r.Komponist)}\b0\par");
                else
                    if (!string.IsNullOrEmpty(r.Werk))
                        sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16\b {EscapeRtf(r.Komponist)}\b0\par");
                else
                    sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16\b {EscapeRtf(r.Werk)}\b0\par");

                // Line 3: Dirigent
                if (!string.IsNullOrEmpty(r.Dirigent))
                    sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16\i {EscapeRtf(r.Dirigent)}\i0\sa50\par");

                // Line 4: Bewertung1 (comment)
                sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16 {EscapeRtf(r.Bewertung1)}\par");

                // Blank line after each record
                sb.AppendLine(@"\par");
            }
        }
        static int LineSpacingTwips(int fsHalfPoints, double multiplier)
        {
            // fsHalfPoints (e.g. 16 for \fs16), each half-point = 10 twips
            return (int)Math.Round(fsHalfPoints * 10 * multiplier);
        }
        private static int CmToTwips(double cm)
        {
            // 1 inch = 2.54 cm, 1 inch = 1440 twips
            double twipsPerCm = 1440.0 / 2.54;
            return (int)Math.Round(cm * twipsPerCm);
        }
        private static string EscapeRtf(string input)
        {
            if (input == null) return "";

            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c == '\\' || c == '{' || c == '}')
                    sb.Append('\\').Append(c);
                else if (c <= 127)
                    sb.Append(c); // ASCII, write as-is
                else
                    sb.Append(@"\u").Append((int)c).Append('?'); // Unicode escape
            }
            return sb.ToString();
        }
        private string ShortDate(string datum)
        {
            return DateTime.TryParse(datum, out var dt) ? dt.ToString("dd.MM.yyyy") : "";
        }
        /// <summary>
        /// Embeds a PNG or JPEG into the RTF StringBuilder.
        /// </summary>
        /// <param name="sb">Your StringBuilder for the RTF.</param>
        /// <param name="imagePath">Path to a .png or .jpg file.</param>
        /// <param name="widthTwips">Desired width in twips (1 in = 1440 twips).</param>
        /// <param name="heightTwips">Desired height in twips.</param>
        private void AppendImageToRtf(StringBuilder sb, string imageName, string? Title, List<string>? info, double dWidthTwips, double dHeightTwips = 0, bool pageBreak = true)
        {
            string imagePath = ImagePath + imageName;

            if (!File.Exists(imagePath))
                return;

            // insert a picture
            sb.AppendLine(@"\page");

            if (Title is not null)
            {
                // 1) Blank line to separate from prior content
                sb.AppendLine(@"\par");

                // 2) Title line: centered, Verdana 10pt bold
                sb.AppendLine($@"\pard\qc\f0\fs24\b {EscapeRtf(Title)}\b0\par");
            }

            // 3) A little extra space before the picture
            sb.AppendLine(@"\pard\par");

            // 4) Center the image
            int widthTwips = (int)dWidthTwips;
            int heightTwips = dHeightTwips == 0 ? (widthTwips * 3) / 2 : (int)dHeightTwips;

            // Read the file
            byte[] bytes = File.ReadAllBytes(imagePath);

            // Hex-encode
            var hex = BitConverter.ToString(bytes).Replace("-", "");

            // Choose control word based on extension
            string pictType = Path.GetExtension(imagePath).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                ? "jpegblip"
                : "pngblip";

            // Insert the pict block
            sb.AppendLine(@"\pard\qc");  // center image; remove if you want left-aligned
            sb.AppendLine("{\\pict\\" + pictType +
                          "\\picwgoal" + widthTwips +
                          "\\pichgoal" + heightTwips + " ");
            sb.AppendLine(hex);
            sb.AppendLine("}\\par");      // close pict and add paragraph

            if (info is not null)
            {
                sb.AppendLine("\\par");      // close pict and add paragraph
                foreach (var n in info)
                {
                    // Line 4: Bewertung1 (comment)
                    sb.AppendLine($@"\pard\ql\li{LeftTab}\sl{LineSpacing}\slmult1\fi0\f0\fs16 {EscapeRtf(n)}\par");
                }
            }

            if (pageBreak)
                sb.AppendLine(@"\page");

        }
        /// <summary>
        /// Appends a typical front‐matter (half‐title, title page, copyright,
        /// dedication, TOC, preface) to the StringBuilder (RTF) before the main content.
        /// </summary>
        void AppendFrontMatter(StringBuilder sb)
        {
            // RTF header
            sb.AppendLine(@"{\rtf1\ansi" +
                @"\paperw6804\paperh10773" +                // page size: 12cm x 19cm
                @"\margl567\margr454\margt567\margb567" +   // margins: L=1cm, R=0.8cm, T=1cm, B=1cm
                @"{\fonttbl\f0\fswiss\fcharset0 Verdana;}");

            // — Stylesheet & Font Table (assumes Verdana defined as \f0 earlier) —
            sb.AppendLine(@"{\stylesheet");
            sb.AppendLine(@"  {\s0 Normal;}\s0");
            sb.AppendLine(@"  {\s1 HalfTitle;\f0\fs48\qr;}");
            sb.AppendLine(@"  {\s2 Title;\f0\fs36\qc;}");
            sb.AppendLine(@"  {\s3 Meta;\f0\fs8\ql;}");
            sb.AppendLine(@"  {\s4 Dedication;\f0\fs20\qc\i;}");
            sb.AppendLine(@"  {\s5 ToCHeading;\f0\fs24\qc\b;}");
            sb.AppendLine(@"  {\s6 ToCEntry;\f0\fs16\ql\li567;}");
            sb.AppendLine(@"  {\s7 Preface;\f0\fs16\ql\li567;}");
            sb.AppendLine(@"}");

            // 1) Half‐Title Page
            sb.AppendLine(@"\pard\s1 Notae Musicae\par");
            sb.AppendLine(@"\page");

            // 3) Copyright / Colophon
            sb.AppendLine($@"\pard\s3 (c) 2025 Kerstin Tru{EscapeRtf("ö")}l. All rights reserved.\par");
            sb.AppendLine(@"\pard\s3 First Edition. Printed in Germany.\par");

            // 2) Title Page
            List<string> list = [
                "September 2022 - Juni 2025",
                $"Kerstin Truöl"
            ];
            AppendImageToRtf(sb, "konzert.jpg", "Notae Musicae", list, (8 * 1440) / 2.54);

            sb.AppendLine(@"\page");

            // 4) Dedication
            sb.AppendLine(@"\pard\s4 To my family, for their endless support.\par");
            sb.AppendLine(@"\page");
           
        }
    }
}
