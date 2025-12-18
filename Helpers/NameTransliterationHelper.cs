using System;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class HindiRomanTransliterator
    {
        // ==================================================
        // 🔥 SIMPLIFIED - No transliteration needed!
        // Whisper already gives us English/romanized text
        // ==================================================
        
        public static string ToRoman(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            Console.WriteLine($"📝 INPUT NAME: '{input}'");

            // Just clean and capitalize - no transliteration!
            var cleaned = CleanName(input);
            var capitalized = CapitalizeWords(cleaned);

            Console.WriteLine($"✅ CLEANED NAME: '{capitalized}'");
            
            return capitalized;
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
            name = Regex.Replace(name, 
                @"^(Mr|Mrs|Ms|Dr|Prof|Shri|Sri|Smt|Kumari|Kumar)\.?\s+", 
                "", 
                RegexOptions.IgnoreCase);

            // Remove trailing punctuation
            name = name.TrimEnd('.', ',', '!', '?', ';', ':');

            // Remove standalone numbers
            name = Regex.Replace(name, @"\s+\d+\s*$", "").Trim();
            name = Regex.Replace(name, @"^\d+\s+", "").Trim();

            // Remove special characters except spaces and hyphens
            name = Regex.Replace(name, @"[^\p{L}\s\-']", "");

            // Clean up multiple spaces
            name = Regex.Replace(name, @"\s+", " ");

            return name.Trim();
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

                // Special cases for common Indian surnames
                var lower = words[i].ToLower();

                words[i] = lower switch
                {
                    // Common surnames
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
                    "sinha" => "Sinha",
                    "mishra" => "Mishra",
                    "pandey" => "Pandey",
                    "tiwari" => "Tiwari",
                    "saxena" => "Saxena",
                    "malhotra" => "Malhotra",
                    "kapoor" => "Kapoor",
                    "chopra" => "Chopra",
                    
                    // Default: capitalize first letter
                    _ => char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower()
                };
            }

            return string.Join(" ", words);
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

        // ==================================================
        // BACKWARD COMPATIBILITY - Keep old methods
        // ==================================================
        public static bool ContainsHindi(string text)
        {
            // Not needed anymore, but keeping for compatibility
            return false;
        }
    }
}