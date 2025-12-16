using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class HindiRomanTransliterator
    {
        // Check Hindi (Devanagari)
        public static bool IsHindi(string text)
        {
            return Regex.IsMatch(text, @"\p{IsDevanagari}");
        }

        // MAIN transliteration (best-effort, never blank)
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var map = new Dictionary<string, string>
            {
                // ---- MATRA COMBINATIONS (HIGH PRIORITY) ----
                ["श्री"] = "Shri",
                ["क्ष"] = "Ksh",
                ["ज्ञ"] = "Gya",

                // ---- VOWELS ----
                ["अ"] = "a", ["आ"] = "aa", ["इ"] = "i", ["ई"] = "ee",
                ["उ"] = "u", ["ऊ"] = "oo", ["ए"] = "e", ["ऐ"] = "ai",
                ["ओ"] = "o", ["औ"] = "au",

                // ---- CONSONANTS ----
                ["क"] = "k", ["ख"] = "kh", ["ग"] = "g", ["घ"] = "gh",
                ["च"] = "ch", ["छ"] = "chh", ["ज"] = "j", ["झ"] = "jh",
                ["ट"] = "t", ["ठ"] = "th", ["ड"] = "d", ["ढ"] = "dh",
                ["त"] = "t", ["थ"] = "th", ["द"] = "d", ["ध"] = "dh",
                ["न"] = "n", ["प"] = "p", ["फ"] = "ph", ["ब"] = "b",
                ["भ"] = "bh", ["म"] = "m", ["य"] = "y", ["र"] = "r",
                ["ल"] = "l", ["व"] = "v", ["श"] = "sh", ["ष"] = "sh",
                ["स"] = "s", ["ह"] = "h",

                // ---- MATRAS ----
                ["ा"] = "a", ["ि"] = "i", ["ी"] = "ee",
                ["ु"] = "u", ["ू"] = "oo", ["े"] = "e",
                ["ै"] = "ai", ["ो"] = "o", ["ौ"] = "au",
                ["ं"] = "n", ["ः"] = "h",

                [" "] = " "
            };

            var sb = new StringBuilder();

            foreach (var ch in input)
            {
                var key = ch.ToString();
                if (map.ContainsKey(key))
                    sb.Append(map[key]);
            }

            var result = sb.ToString();

            // cleanup
            result = Regex.Replace(result, @"\s+", " ").Trim();

            // Capitalize words
            result = CultureCapitalize(result);

            return result;
        }

        private static string CultureCapitalize(string text)
        {
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 1)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }
            return string.Join(" ", words);
        }
    }
}
