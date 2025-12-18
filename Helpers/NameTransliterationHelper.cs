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
        // 🔥 CHECK IF TEXT IS ALREADY IN ROMAN/ENGLISH
        // ==================================================
        private static bool IsAlreadyRoman(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // If it's purely ASCII letters and spaces, it's already Roman
            return Regex.IsMatch(text, @"^[a-zA-Z\s]+$");
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

            // 🔥 FIX: If already Roman (pure ASCII), just clean and capitalize
            if (IsAlreadyRoman(input))
            {
                Console.WriteLine($"✅ Already Roman: {input}");
                return CapitalizeWords(input);
            }

            // If no Hindi characters, just clean and capitalize
            if (!ContainsHindi(input))
            {
                Console.WriteLine($"✅ No Hindi: {input}");
                return CapitalizeWords(input);
            }

            Console.WriteLine($"🔄 Transliterating: {input}");

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
            result = CleanupTransliteration(result);
            result = CapitalizeWords(result);
            
            Console.WriteLine($"✅ Result: {result}");
            return result;
        }

        // ==================================================
        // 🔥 CORE TRANSLITERATION - IMPROVED ALGORITHM
        // ==================================================
        private static string TransliterateWord(string word)
        {
            var sb = new StringBuilder();
            int i = 0;

            while (i < word.Length)
            {
                char current = word[i];
                
                // Look ahead for modifiers
                char next = (i + 1 < word.Length) ? word[i + 1] : '\0';
                char nextNext = (i + 2 < word.Length) ? word[i + 2] : '\0';

                // Handle conjuncts (consonant + halant + consonant)
                if (next == '्' && nextNext != '\0')
                {
                    // This is a conjunct: current + halant + nextNext
                    string consonant1 = MapConsonant(current);
                    string consonant2 = MapConsonant(nextNext);
                    
                    if (!string.IsNullOrEmpty(consonant1) && !string.IsNullOrEmpty(consonant2))
                    {
                        sb.Append(consonant1);
                        sb.Append(consonant2);
                        i += 3; // Skip all three characters
                        continue;
                    }
                }

                // Handle consonant + matra
                if (IsConsonant(current) && IsMatra(next))
                {
                    string consonant = MapConsonant(current);
                    string vowel = MapMatra(next);
                    
                    sb.Append(consonant);
                    sb.Append(vowel);
                    i += 2;
                    continue;
                }

                // Handle standalone consonant (adds inherent 'a')
                if (IsConsonant(current))
                {
                    string consonant = MapConsonant(current);
                    sb.Append(consonant);
                    sb.Append('a'); // Inherent 'a'
                    i++;
                    continue;
                }

                // Handle standalone vowel
                if (IsVowel(current))
                {
                    string vowel = MapVowel(current);
                    sb.Append(vowel);
                    i++;
                    continue;
                }

                // Handle other characters
                var mapped = MapSingleChar(current);
                if (!string.IsNullOrEmpty(mapped))
                {
                    sb.Append(mapped);
                }
                else if (char.IsLetter(current))
                {
                    sb.Append(current);
                }

                i++;
            }

            return sb.ToString();
        }

        // ==================================================
        // 🔥 HELPER: CHECK CHARACTER TYPES
        // ==================================================
        private static bool IsConsonant(char ch)
        {
            return ch >= 'क' && ch <= 'ह';
        }

        private static bool IsVowel(char ch)
        {
            return ch >= 'अ' && ch <= 'औ';
        }

        private static bool IsMatra(char ch)
        {
            return (ch >= 'ा' && ch <= 'ौ') || ch == 'ं' || ch == 'ँ' || ch == 'ः';
        }

        // ==================================================
        // 🔥 IMPROVED MAPPING FUNCTIONS
        // ==================================================
        private static string MapConsonant(char ch)
        {
            return ch switch
            {
                'क' => "k",
                'ख' => "kh",
                'ग' => "g",
                'घ' => "gh",
                'ङ' => "ng",
                'च' => "ch",
                'छ' => "chh",
                'ज' => "j",
                'झ' => "jh",
                'ञ' => "ny",
                'ट' => "t",
                'ठ' => "th",
                'ड' => "d",
                'ढ' => "dh",
                'ण' => "n",
                'त' => "t",
                'थ' => "th",
                'द' => "d",
                'ध' => "dh",
                'न' => "n",
                'प' => "p",
                'फ' => "ph",
                'ब' => "b",
                'भ' => "bh",
                'म' => "m",
                'य' => "y",
                'र' => "r",
                'ल' => "l",
                'व' => "v",
                'ळ' => "l",
                'श' => "sh",
                'ष' => "sh",
                'स' => "s",
                'ह' => "h",
                _ => null
            };
        }

        private static string MapVowel(char ch)
        {
            return ch switch
            {
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
                _ => null
            };
        }

        private static string MapMatra(char ch)
        {
            return ch switch
            {
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
                'ं' => "n",
                'ँ' => "n",
                'ः' => "h",
                '्' => "", // halant
                _ => null
            };
        }

        private static string MapSingleChar(char ch)
        {
            return ch switch
            {
                'ं' => "n",
                'ँ' => "n",
                'ः' => "h",
                '्' => "",
                _ => null
            };
        }

        // ==================================================
        // 🔥 AGGRESSIVE CLEANUP - REMOVE DOUBLE VOWELS
        // ==================================================
        private static string CleanupTransliteration(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // 🔥 CRITICAL FIX: Remove double vowels more aggressively
            // "Bhaoomaikaaa" → "Bhoomika"
            
            // Step 1: Replace triple+ vowels with double
            text = Regex.Replace(text, @"a{3,}", "a");
            text = Regex.Replace(text, @"e{3,}", "e");
            text = Regex.Replace(text, @"i{3,}", "i");
            text = Regex.Replace(text, @"o{3,}", "o");
            text = Regex.Replace(text, @"u{3,}", "u");

            // Step 2: Smart cleanup - keep only necessary double vowels
            // "aa", "ee", "oo" are valid in Hindi transliteration
            // But "aaa", "ooo" are not
            
            // Replace patterns like "ooa" with "oo" or "ua"
            text = Regex.Replace(text, @"([aeiou])\1+([aeiou])", m =>
            {
                string firstVowel = m.Groups[1].Value;
                string nextVowel = m.Groups[2].Value;
                
                // If same vowel repeated, keep only two
                if (firstVowel == nextVowel)
                    return firstVowel + firstVowel;
                
                // Different vowels: keep one of each
                return firstVowel + nextVowel;
            });

            // Step 3: Final cleanup
            text = text.Replace("aaa", "a");
            text = text.Replace("eee", "e");
            text = text.Replace("iii", "i");
            text = text.Replace("ooo", "o");
            text = text.Replace("uuu", "u");

            return text.Trim();
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
        // 🔥 CLEAN NAME - REMOVE NOISE
        // ==================================================
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            // Remove extra spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            // Remove titles
            name = Regex.Replace(name, @"^(Mr|Mrs|Ms|Dr|Prof|Shri|Sri|Smt|Kumari|Kumar|श्री|श्रीमती)\.?\s+", "", RegexOptions.IgnoreCase);

            // Remove trailing punctuation
            name = name.TrimEnd('.', ',', '!', '?', ';', ':');

            // Remove standalone numbers
            name = Regex.Replace(name, @"\s+\d+\s*$", "").Trim();
            name = Regex.Replace(name, @"^\d+\s+", "").Trim();

            // Remove special characters except spaces
            name = Regex.Replace(name, @"[^\p{L}\s]", "");

            // Clean up multiple spaces
            name = Regex.Replace(name, @"\s+", " ");

            return name;
        }

        // ==================================================
        // 🔥 VALIDATE NAME
        // ==================================================
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.Length < 2)
                return false;

            if (!Regex.IsMatch(name, @"[\p{L}]"))
                return false;

            if (Regex.IsMatch(name, @"^\d+$"))
                return false;

            return true;
        }
    }
}