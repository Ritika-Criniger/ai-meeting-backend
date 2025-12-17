using System;
using System.Collections.Generic;
using System.Linq;
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
        // 🔥 IMPROVED MAIN ENTRY POINT
        // ==================================================
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            // Remove common titles (noise)
            input = Regex.Replace(input, @"\b(Mr|Mrs|Ms|Dr|Shri|Sri)\.?\s*", "", RegexOptions.IgnoreCase);

            // 🔥 CHECK IF NAME IS ALREADY IN ENGLISH
            // If no Hindi characters found, just clean and return
            if (!ContainsHindi(input))
            {
                // Clean up and capitalize properly
                input = Regex.Replace(input, @"\s+", " ").Trim();
                return CapitalizeWords(input);
            }

            // Process mixed or Hindi names
            var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var resultWords = new List<string>();

            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // If word has Hindi, transliterate it
                if (ContainsHindi(trimmed))
                    resultWords.Add(ToRomanWord(trimmed));
                else
                    resultWords.Add(trimmed); // Keep English as-is
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
                ["श्री"] = "Shri",
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
                ["न"] = "n", ["ण"] = "n",
                ["प"] = "p", ["फ"] = "ph", ["ब"] = "b", ["भ"] = "bh",
                ["म"] = "m",
                ["य"] = "y", ["र"] = "r", ["ल"] = "l", ["व"] = "v", ["ळ"] = "l",
                ["श"] = "sh", ["ष"] = "sh", ["स"] = "s",
                ["ह"] = "h",

                // ---- MATRAS (vowel signs) ----
                ["ा"] = "a",    // ा makes 'aa' sound but we write as 'a' for natural spelling
                ["ि"] = "i", 
                ["ी"] = "i",    // ी makes 'ee' but we write as 'i' (Rajesh not Rajeesh)
                ["ु"] = "u", 
                ["ू"] = "u",    // ू makes 'oo' but we write as 'u' 
                ["ृ"] = "ri",
                ["े"] = "e", 
                ["ै"] = "ai",
                ["ो"] = "o", 
                ["ौ"] = "au",
                ["ॉ"] = "o",    // ऑ candra o
                ["्य"] = "ya",  // य् + vowel

                // ---- MODIFIERS ----
                ["ं"] = "n",   // anusvara
                ["ँ"] = "n",   // chandrabindu
                ["ः"] = "h",   // visarga
                ["्"] = ""     // halant (removes inherent 'a')
            };

            var sb = new StringBuilder();
            int i = 0;

            while (i < word.Length)
            {
                bool matched = false;

                // Try 2-char combinations first (conjuncts)
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

                // Try single char
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
        // 🔥 SMART CAPITALIZATION (PRESERVES ENGLISH NAMES)
        // ==================================================
        private static string CapitalizeWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;

                var lower = words[i].ToLower();

                // Special case for common Indian titles/names
                if (lower == "shri" || lower == "sri")
                    words[i] = "Shri";
                else if (lower == "kumar")
                    words[i] = "Kumar";
                else if (lower == "singh")
                    words[i] = "Singh";
                else if (lower == "sharma")
                    words[i] = "Sharma";
                else if (lower == "verma")
                    words[i] = "Verma";
                else if (lower == "gupta")
                    words[i] = "Gupta";
                // 🔥 Just capitalize first letter for all other names
                else
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }

            return string.Join(" ", words);
        }

        // ==================================================
        // 🔥 NEW: CLEAN NAME (REMOVE EXTRA SPACES, TRIM)
        // ==================================================
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            // Remove extra spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            // Remove titles
            name = Regex.Replace(name, @"^(Mr|Mrs|Ms|Dr|Shri|Sri)\.?\s+", "", RegexOptions.IgnoreCase);

            // Remove trailing punctuation
            name = name.TrimEnd('.', ',', '!', '?');

            return name;
        }
    }
}