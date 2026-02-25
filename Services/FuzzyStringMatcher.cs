using System;
using System.Text.RegularExpressions;

namespace MaestroNotes.Services
{
    public static class FuzzyStringMatcher
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            string s = input.ToLowerInvariant();

            // Handle umlauts
            s = s.Replace("ä", "ae");
            s = s.Replace("ö", "oe");
            s = s.Replace("ü", "ue");

            // Handle prefixes
            s = s.Replace("van ", "v ");
            s = s.Replace("von ", "v ");
            s = s.Replace("v. ", "v ");

            // Handle specific character combinations
            // Order matters! Longest first.
            s = s.Replace("tsch", "ch");
            s = s.Replace("tch", "ch");
            s = s.Replace("sch", "sh");
            s = s.Replace("ß", "ss");
            s = s.Replace("ph", "f");
            s = s.Replace("kh", "ch"); // Rachmaninoff vs Rakhmaninov

            // Handle single character replacements
            s = s.Replace('z', 's');
            s = s.Replace('w', 'v');

            // Remove non-alphanumeric characters (keep spaces)
            // Using regex to remove punctuation
            s = Regex.Replace(s, @"[^\w\s]", "");

            // Collapse multiple spaces
            s = Regex.Replace(s, @"\s+", " ").Trim();

            return s;
        }

        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }
            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        public static bool IsMatch(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return false;

            // Direct match check first for speed
            if (s1.Equals(s2, StringComparison.OrdinalIgnoreCase)) return true;

            string n1 = Normalize(s1);
            string n2 = Normalize(s2);

            if (n1 == n2) return true;

            int dist = LevenshteinDistance(n1, n2);
            int len = Math.Max(n1.Length, n2.Length);

            // Threshold logic
            if (len <= 4) return dist == 0; // Strict for short words
            if (len <= 8) return dist <= 1; // Allow 1 typo for medium words
            return dist <= 2; // Allow 2 typos for longer words
        }
    }
}
