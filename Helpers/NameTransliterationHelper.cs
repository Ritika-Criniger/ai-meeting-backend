using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class HindiRomanTransliterator
    {
        // ==================================================
        // CHECK IF TEXT CONTAINS HINDI (DEVANAGARI)
        // ==================================================
        public static bool ContainsHindi(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text, @"[\u0900-\u097F]");
        }

        // ==================================================
        // MAIN ENTRY POINT
        // ==================================================
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            // Remove common titles (noise)
            input = Regex.Replace(input, @"\b(Mr|Mrs|Ms|Dr)\.?\b", "", RegexOptions.IgnoreCase);

            var words = input.Split(' ');
            var resultWords = new List<string>();

            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (ContainsHindi(trimmed))
                    resultWords.Add(ToRomanWord(trimmed));
                else
                    resultWords.Add(trimmed);
            }

            var result = string.Join(" ", resultWords);
            result = Regex.Replace(result, @"\s+", " ").Trim();

            return CapitalizeWords(result);
        }

        // ==================================================
        // CORE WORD TRANSLITERATION (NO DUPLICATES)
        // ==================================================
        private static string ToRomanWord(string word)
        {
            var map = new Dictionary<string, string>
            {
                // ---- SPECIAL CONJUNCTS ----
                ["श्री"] = "shri",
                ["क्ष"] = "ksh",
                ["ज्ञ"] = "gya",
                ["त्र"] = "tr",

                // ---- VOWELS ----
                ["अ"] = "a", ["आ"] = "aa", ["इ"] = "i", ["ई"] = "ee",
                ["उ"] = "u", ["ऊ"] = "oo", ["ऋ"] = "ri",
                ["ए"] = "e", ["ऐ"] = "ai", ["ओ"] = "o", ["औ"] = "au",

                // ---- CONSONANTS ----
                ["क"] = "k", ["ख"] = "kh", ["ग"] = "g", ["घ"] = "gh",
                ["च"] = "ch", ["छ"] = "chh", ["ज"] = "j", ["झ"] = "jh",
                ["ट"] = "t", ["ठ"] = "th", ["ड"] = "d", ["ढ"] = "dh",
                ["त"] = "t", ["थ"] = "th", ["द"] = "d", ["ध"] = "dh",
                ["न"] = "n",
                ["प"] = "p", ["फ"] = "ph", ["ब"] = "b", ["भ"] = "bh",
                ["म"] = "m",
                ["य"] = "y", ["र"] = "r", ["ल"] = "l", ["व"] = "v",
                ["श"] = "sh", ["ष"] = "sh", ["स"] = "s",
                ["ह"] = "h",

                // ---- MATRAS ----
                ["ा"] = "a",
                ["ि"] = "i", ["ी"] = "ee",
                ["ु"] = "u", ["ू"] = "oo",
                ["ृ"] = "ri",
                ["े"] = "e", ["ै"] = "ai",
                ["ो"] = "o", ["ौ"] = "au",

                // ---- MODIFIERS (FIXED – NO DUPLICATES) ----
                ["ं"] = "n",   // anusvara
                ["ँ"] = "n",   // chandrabindu
                ["ः"] = "h",
                ["्"] = ""     // halant
            };

            var sb = new StringBuilder();
            int i = 0;

            while (i < word.Length)
            {
                bool matched = false;

                // Try 2-char combinations first
                if (i + 1 < word.Length)
                {
                    var two = word.Substring(i, 2);
                    if (map.ContainsKey(two))
                    {
                        sb.Append(map[two]);
                        i += 2;
                        matched = true;
                        continue;
                    }
                }

                var one = word[i].ToString();
                if (map.ContainsKey(one))
                {
                    sb.Append(map[one]);
                    matched = true;
                }
                else
                {
                    sb.Append(word[i]); // keep unknown chars
                }

                i++;
            }

            var result = sb.ToString();

            // Cleanup repeated vowels
            result = Regex.Replace(result, @"aa+", "aa");
            result = Regex.Replace(result, @"ee+", "ee");
            result = Regex.Replace(result, @"oo+", "oo");

            return result;
        }

        // ==================================================
        // SMART CAPITALIZATION
        // ==================================================
        private static string CapitalizeWords(string text)
        {
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;

                var lower = words[i].ToLower();

                if (lower == "shri")
                    words[i] = "Shri";
                else if (lower == "kumar")
                    words[i] = "Kumar";
                else if (lower == "singh")
                    words[i] = "Singh";
                else if (lower == "sharma")
                    words[i] = "Sharma";
                else
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }

            return string.Join(" ", words);
        }
    }
}
