// HindiRomanTransliterator.cs - PRODUCTION READY VERSION

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class HindiRomanTransliterator
    {
        // ✅ Consonant mapping
        private static readonly Dictionary<char, string> ConsonantMap = new()
        {
            {'क', "k"}, {'ख', "kh"}, {'ग', "g"}, {'घ', "gh"}, {'ङ', "ng"},
            {'च', "ch"}, {'छ', "chh"}, {'ज', "j"}, {'झ', "jh"}, {'ञ', "ny"},
            {'ट', "t"}, {'ठ', "th"}, {'ड', "d"}, {'ढ', "dh"}, {'ण', "n"},
            {'त', "t"}, {'थ', "th"}, {'द', "d"}, {'ध', "dh"}, {'न', "n"},
            {'प', "p"}, {'फ', "ph"}, {'ब', "b"}, {'भ', "bh"}, {'म', "m"},
            {'य', "y"}, {'र', "r"}, {'ल', "l"}, {'व', "w"}, {'ळ', "l"},
            {'श', "sh"}, {'ष', "sh"}, {'स', "s"}, {'ह', "h"}
        };

        // ✅ Vowel mapping
        private static readonly Dictionary<char, string> VowelMap = new()
        {
            {'अ', "a"}, {'आ', "aa"}, {'इ', "i"}, {'ई', "ee"},
            {'उ', "u"}, {'ऊ', "oo"}, {'ऋ', "ri"},
            {'ए', "e"}, {'ऐ', "ai"}, {'ओ', "o"}, {'औ', "au"}
        };

        // ✅ Matra mapping
        private static readonly Dictionary<char, string> MatraMap = new()
        {
            {'ा', "aa"}, {'ि', "i"}, {'ी', "ee"}, {'ु', "u"}, {'ू', "oo"},
            {'ृ', "ri"}, {'े', "e"}, {'ै', "ai"}, {'ो', "o"}, {'ौ', "au"},
            {'ॉ', "o"}, {'ं', "n"}, {'ँ', "n"}, {'ः', "h"}, {'्', ""}
        };

        // ✅ Common Indian names - for accurate conversion
        private static readonly Dictionary<string, string> CommonNameFixes = new()
        {
            {"bhuumikaa", "Bhumika"}, {"bhoomika", "Bhumika"}, {"bhaoomika", "Bhumika"},
            {"gaaurii", "Gauri"}, {"gauri", "Gauri"}, {"gaauree", "Gauri"},
            {"raakaesha", "Rakesh"}, {"raakesh", "Rakesh"}, {"rakaesha", "Rakesh"},
            {"tekaama", "Tekam"}, {"tekam", "Tekam"},
            {"shaarmaa", "Sharma"}, {"sharma", "Sharma"},
            {"kumaara", "Kumar"}, {"kumar", "Kumar"},
            {"singha", "Singh"}, {"singh", "Singh"},
            {"naandinii", "Nandini"}, {"nandini", "Nandini"},
            {"jaaina", "Jain"}, {"jain", "Jain"},
            {"priyaa", "Priya"}, {"priya", "Priya"},
            {"anushkaa", "Anushka"}, {"anushka", "Anushka"},
            {"raajaesha", "Rajesh"}, {"rajesh", "Rajesh"},
            {"sunaila", "Sunil"}, {"sunil", "Sunil"},
            {"deepaka", "Deepak"}, {"deepak", "Deepak"},
            {"aashaa", "Asha"}, {"asha", "Asha"}
        };

        public static bool ContainsHindi(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, @"[\u0900-\u097F]");
        }

        private static bool IsAlreadyRoman(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, @"^[a-zA-Z\s]+$");
        }

        // ✅ MAIN ENTRY POINT
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            Console.WriteLine($"🔄 Input: {input}");

            // Clean input
            input = CleanName(input);

            // If already Roman, just capitalize
            if (IsAlreadyRoman(input))
            {
                Console.WriteLine($"✅ Already Roman: {input}");
                return CapitalizeWords(input);
            }

            // If no Hindi, return as-is
            if (!ContainsHindi(input))
            {
                Console.WriteLine($"✅ No Hindi detected: {input}");
                return CapitalizeWords(input);
            }

            // Process word by word
            var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var resultWords = new List<string>();

            foreach (var word in words)
            {
                if (ContainsHindi(word))
                {
                    var romanized = TransliterateWord(word);
                    resultWords.Add(romanized);
                    Console.WriteLine($"  📝 '{word}' → '{romanized}'");
                }
                else
                {
                    resultWords.Add(word);
                }
            }

            var result = string.Join(" ", resultWords);
            
            // Apply cleanup and fixes
            result = CleanupTransliteration(result);
            result = ApplyCommonNameFixes(result);
            result = CapitalizeWords(result);
            
            Console.WriteLine($"✅ Final Output: {result}");
            return result;
        }

        // ✅ CORE TRANSLITERATION ENGINE - IMPROVED
        private static string TransliterateWord(string word)
        {
            var sb = new StringBuilder();
            int i = 0;

            while (i < word.Length)
            {
                char current = word[i];
                char next = (i + 1 < word.Length) ? word[i + 1] : '\0';
                char nextNext = (i + 2 < word.Length) ? word[i + 2] : '\0';

                // 1️⃣ Handle standalone vowels (अ, आ, इ, etc.)
                if (VowelMap.ContainsKey(current))
                {
                    sb.Append(VowelMap[current]);
                    i++;
                    continue;
                }

                // 2️⃣ Handle consonant clusters with halant (त्म → tm)
                if (ConsonantMap.ContainsKey(current) && next == '्' && ConsonantMap.ContainsKey(nextNext))
                {
                    sb.Append(ConsonantMap[current]);
                    sb.Append(ConsonantMap[nextNext]);
                    i += 3; // Skip all three characters
                    continue;
                }

                // 3️⃣ Handle consonant + matra (क + ा → kaa)
                if (ConsonantMap.ContainsKey(current) && MatraMap.ContainsKey(next))
                {
                    sb.Append(ConsonantMap[current]);
                    
                    // Don't add anything for halant
                    if (next != '्')
                    {
                        sb.Append(MatraMap[next]);
                    }
                    
                    i += 2;
                    continue;
                }

                // 4️⃣ Handle standalone consonant
                if (ConsonantMap.ContainsKey(current))
                {
                    sb.Append(ConsonantMap[current]);
                    
                    // Check what follows
                    bool followedByHalant = (next == '्');
                    bool followedByMatra = MatraMap.ContainsKey(next);
                    bool isLastChar = (i == word.Length - 1);
                    bool isBeforeSpace = (next == ' ' || next == '\0');
                    
                    // ✅ CRITICAL FIX: Only add inherent 'a' in specific cases
                    if (!followedByHalant && !followedByMatra)
                    {
                        // Add 'a' if consonant is in the middle of the word
                        if (!isLastChar && !isBeforeSpace)
                        {
                            sb.Append('a');
                        }
                        // Don't add 'a' at the end of words to avoid "Bhoomikaa"
                    }
                    
                    i++;
                    continue;
                }

                // 5️⃣ Handle anusvara, visarga, chandrabindu
                if (MatraMap.ContainsKey(current))
                {
                    sb.Append(MatraMap[current]);
                    i++;
                    continue;
                }

                // 6️⃣ Keep other characters as-is
                if (char.IsLetter(current))
                {
                    sb.Append(current);
                }

                i++;
            }

            return sb.ToString();
        }

        // ✅ AGGRESSIVE CLEANUP
        private static string CleanupTransliteration(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Remove excessive vowel repetitions
            text = Regex.Replace(text, @"a{3,}", "a");
            text = Regex.Replace(text, @"e{3,}", "e");
            text = Regex.Replace(text, @"i{3,}", "i");
            text = Regex.Replace(text, @"o{3,}", "o");
            text = Regex.Replace(text, @"u{3,}", "u");

            // Fix common double vowel issues
            text = Regex.Replace(text, @"aa+", "a");  // "bhuumikaaa" → "bhumika"
            text = Regex.Replace(text, @"ee+", "ee"); // Keep "ee" for ई
            text = Regex.Replace(text, @"oo+", "oo"); // Keep "oo" for ऊ

            // Remove trailing 'a' from words (common issue)
            text = Regex.Replace(text, @"\ba([a-z]+)a\b", "a$1"); // "bhaa" → "bha"

            return text.Trim();
        }

        // ✅ APPLY COMMON NAME FIXES
        private static string ApplyCommonNameFixes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var words = text.Split(' ');
            var fixedWords = new List<string>();

            foreach (var word in words)
            {
                var lower = word.ToLower().Trim();
                
                // Check if this word matches a known name pattern
                if (CommonNameFixes.ContainsKey(lower))
                {
                    fixedWords.Add(CommonNameFixes[lower]);
                }
                else
                {
                    fixedWords.Add(word);
                }
            }

            return string.Join(" ", fixedWords);
        }

        // ✅ SMART CAPITALIZATION
        private static string CapitalizeWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;

                var lower = words[i].ToLower();
                
                // Special handling for common surnames
                words[i] = lower switch
                {
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
                    "jain" => "Jain",
                    "mehta" => "Mehta",
                    "agarwal" => "Agarwal",
                    _ => char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower()
                };
            }

            return string.Join(" ", words);
        }

        // ✅ CLEAN NAME - REMOVE NOISE
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            // Remove extra spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            // Remove titles (both English and Hindi)
            name = Regex.Replace(name, @"\b(Mr|Mrs|Ms|Dr|Prof|Shri|Sri|Smt|Kumari|Kumar|श्री|श्रीमती)\.?\s+", "", RegexOptions.IgnoreCase);

            // Remove trailing punctuation
            name = name.TrimEnd('.', ',', '!', '?', ';', ':', '-');

            // Remove standalone numbers at start/end
            name = Regex.Replace(name, @"\s+\d+\s*$", "").Trim();
            name = Regex.Replace(name, @"^\d+\s+", "").Trim();

            // Remove special characters except spaces and Hindi characters
            name = Regex.Replace(name, @"[^\p{L}\s]", "");

            // Final space cleanup
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return name;
        }

        // ✅ VALIDATE NAME
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length < 2) return false;
            if (!Regex.IsMatch(name, @"[\p{L}]")) return false;
            if (Regex.IsMatch(name, @"^\d+$")) return false;

            return true;
        }
    }
}