using System;
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
        // 🔥 MAIN ENTRY POINT - SMART TRANSLITERATION
        // ==================================================
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            // Remove common titles
            input = Regex.Replace(input, @"\b(Mr|Mrs|Ms|Dr|Shri|Sri|श्री)\.?\s*", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\s+", " ").Trim();

            // If no Hindi characters, just clean and capitalize
            if (!ContainsHindi(input))
            {
                return CapitalizeWords(input);
            }

            // Process each word separately
            var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var resultWords = new List<string>();

            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // If word has Hindi, transliterate it
                if (ContainsHindi(trimmed))
                {
                    var romanized = TransliterateWord(trimmed);
                    resultWords.Add(romanized);
                }
                else
                {
                    // Keep English words as-is
                    resultWords.Add(trimmed);
                }
            }

            var result = string.Join(" ", resultWords);
            return CapitalizeWords(result);
        }

        // ==================================================
        // 🔥 CORE TRANSLITERATION - CHARACTER BY CHARACTER
        // ==================================================
        private static string TransliterateWord(string word)
        {
            var sb = new StringBuilder();
            int i = 0;

            while (i < word.Length)
            {
                // Try 3-character conjuncts first
                if (i + 2 < word.Length)
                {
                    var three = word.Substring(i, 3);
                    var threeResult = MapThreeChar(three);
                    if (!string.IsNullOrEmpty(threeResult))
                    {
                        sb.Append(threeResult);
                        i += 3;
                        continue;
                    }
                }

                // Try 2-character conjuncts
                if (i + 1 < word.Length)
                {
                    var two = word.Substring(i, 2);
                    var twoResult = MapTwoChar(two);
                    if (!string.IsNullOrEmpty(twoResult))
                    {
                        sb.Append(twoResult);
                        i += 2;
                        continue;
                    }
                }

                // Try single character
                var one = word[i];
                var oneResult = MapSingleChar(one);
                if (!string.IsNullOrEmpty(oneResult))
                {
                    sb.Append(oneResult);
                }
                else if (char.IsLetter(one))
                {
                    // Keep unknown letters as-is
                    sb.Append(one);
                }

                i++;
            }

            return CleanupTransliteration(sb.ToString());
        }

        // ==================================================
        // 🔥 CHARACTER MAPPING FUNCTIONS
        // ==================================================
        private static string MapThreeChar(string chars)
        {
            return chars switch
            {
                "क्ष" => "ksh",
                "त्र" => "tra",
                "ज्ञ" => "gya",
                "श्र" => "shr",
                _ => null
            };
        }

        private static string MapTwoChar(string chars)
        {
            return chars switch
            {
                // Conjuncts with halant
                "क्" => "k",
                "ख्" => "kh",
                "ग्" => "g",
                "घ्" => "gh",
                "च्" => "ch",
                "छ्" => "chh",
                "ज्" => "j",
                "झ्" => "jh",
                "ट्" => "t",
                "ठ्" => "th",
                "ड्" => "d",
                "ढ्" => "dh",
                "त्" => "t",
                "थ्" => "th",
                "द्" => "d",
                "ध्" => "dh",
                "न्" => "n",
                "प्" => "p",
                "फ्" => "ph",
                "ब्" => "b",
                "भ्" => "bh",
                "म्" => "m",
                "य्" => "y",
                "र्" => "r",
                "ल्" => "l",
                "व्" => "v",
                "श्" => "sh",
                "ष्" => "sh",
                "स्" => "s",
                "ह्" => "h",
                _ => null
            };
        }

        private static string MapSingleChar(char ch)
        {
            return ch switch
            {
                // Vowels
                'अ' => "a",
                'आ' => "aa",
                'इ' => "i",
                'ई' => "ee",
                'उ' => "u",
                'ऊ' => "oo",
                'ऋ' => "ri",
                'ए' => "e",
                'ऐ' => "ai",
                'ओ' => "o",
                'औ' => "au",

                // Consonants (with inherent 'a')
                'क' => "ka",
                'ख' => "kha",
                'ग' => "ga",
                'घ' => "gha",
                'ङ' => "nga",
                'च' => "cha",
                'छ' => "chha",
                'ज' => "ja",
                'झ' => "jha",
                'ञ' => "nya",
                'ट' => "ta",
                'ठ' => "tha",
                'ड' => "da",
                'ढ' => "dha",
                'ण' => "na",
                'त' => "ta",
                'थ' => "tha",
                'द' => "da",
                'ध' => "dha",
                'न' => "na",
                'प' => "pa",
                'फ' => "pha",
                'ब' => "ba",
                'भ' => "bha",
                'म' => "ma",
                'य' => "ya",
                'र' => "ra",
                'ल' => "la",
                'व' => "va",
                'ळ' => "la",
                'श' => "sha",
                'ष' => "sha",
                'स' => "sa",
                'ह' => "ha",

                // Matras (vowel signs)
                'ा' => "aa",
                'ि' => "i",
                'ी' => "ee",
                'ु' => "u",
                'ू' => "oo",
                'ृ' => "ri",
                'े' => "e",
                'ै' => "ai",
                'ो' => "o",
                'ौ' => "au",
                'ॉ' => "o",

                // Modifiers
                'ं' => "n",
                'ँ' => "n",
                'ः' => "h",
                '्' => "", // halant (removes inherent 'a')

                _ => null
            };
        }

        // ==================================================
        // 🔥 CLEANUP TRANSLITERATION - FIXED
        // ==================================================
        private static string CleanupTransliteration(string text)
        {
            // Only reduce excessive repetitions (3+ consecutive)
            // This preserves correct names like "Jeeta", "Bhoot"
            text = Regex.Replace(text, @"a{3,}", "aa");
            text = Regex.Replace(text, @"e{3,}", "ee");
            text = Regex.Replace(text, @"i{3,}", "ii");
            text = Regex.Replace(text, @"o{3,}", "oo");
            text = Regex.Replace(text, @"u{3,}", "uu");

            // ❌ REMOVED: Aggressive vowel simplification that destroyed names
            // text = text.Replace("aa", "a");
            // text = text.Replace("ee", "i");
            // text = text.Replace("oo", "u");

            // Only simplify when we have excessive repetition
            text = Regex.Replace(text, @"([aeiou])\1{3,}", "$1$1"); // Max 2 repetitions

            return text;
        }

        // ==================================================
        // 🔥 SMART CAPITALIZATION
        // ==================================================
        private static string CapitalizeWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;

                // Special cases for common Indian names/titles
                var lower = words[i].ToLower();

                words[i] = lower switch
                {
                    "shri" or "sri" => "Shri",
                    "kumar" => "Kumar",
                    "singh" => "Singh",
                    "sharma" => "Sharma",
                    "verma" => "Verma",
                    "gupta" => "Gupta",
                    "patel" => "Patel",
                    "shah" => "Shah",
                    "khan" => "Khan",
                    "reddy" => "Reddy",
                    "rao" => "Rao",
                    "nair" => "Nair",
                    "pillai" => "Pillai",
                    "iyer" => "Iyer",
                    "yadav" => "Yadav",
                    "jain" => "Jain",
                    "mehta" => "Mehta",
                    "agarwal" or "aggarwal" => "Agarwal",
                    _ => char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower()
                };
            }

            return string.Join(" ", words);
        }

        // ==================================================
        // 🔥 CLEAN NAME - REMOVE NOISE - ENHANCED
        // ==================================================
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            // Remove extra spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            // Remove titles (more comprehensive)
            name = Regex.Replace(name, @"^(Mr|Mrs|Ms|Dr|Prof|Shri|Sri|Smt|Kumari|Kumar|श्री|श्रीमती)\.?\s+", "", RegexOptions.IgnoreCase);

            // Remove trailing punctuation
            name = name.TrimEnd('.', ',', '!', '?', ';', ':');

            // Remove standalone numbers but preserve if part of name
            // This allows "Rajesh 123" but removes "Rajesh 123 456"
            name = Regex.Replace(name, @"\s+\d+\s*$", "").Trim();
            name = Regex.Replace(name, @"^\d+\s+", "").Trim();

            // Clean up multiple spaces again
            name = Regex.Replace(name, @"\s+", " ");

            return name;
        }

        // ==================================================
        // 🔥 VALIDATE NAME (OPTIONAL HELPER) - ENHANCED
        // ==================================================
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Name should have at least 2 characters
            if (name.Length < 2)
                return false;

            // Name should have at least one letter
            if (!Regex.IsMatch(name, @"[\p{L}]"))
                return false;

            // Name shouldn't be all numbers
            if (Regex.IsMatch(name, @"^\d+$"))
                return false;

            return true;
        }
    }
}