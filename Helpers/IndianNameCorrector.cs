// ============================================================
// FILE 1: Helpers/IndianNameCorrector.cs (NEW FILE)
// Location: D:\crm-ai-agent\AiMeetingBackend\Helpers\IndianNameCorrector.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class IndianNameCorrector
    {
        // First Names - Phonetic variations
        private static readonly Dictionary<string, string> FirstNameCorrections = new()
        {
            // Male Names
            {"naraj", "Neeraj"}, {"neeraj", "Neeraj"}, {"niraj", "Neeraj"},
            {"rakaesha", "Rakesh"}, {"raakesh", "Rakesh"}, {"rakesh", "Rakesh"},
            {"vikram", "Vikram"}, {"vikraam", "Vikram"}, {"vikran", "Vikrant"},
            {"vikrant", "Vikrant"}, {"vikraan", "Vikrant"}, {"vikraant", "Vikrant"},
            {"nilesh", "Nilesh"}, {"neelesh", "Nilesh"}, {"neilesh", "Nilesh"},
            {"rajesh", "Rajesh"}, {"raajesh", "Rajesh"}, {"rajesha", "Rajesh"},
            {"sunil", "Sunil"}, {"sunail", "Sunil"}, {"sunaila", "Sunil"},
            {"deepak", "Deepak"}, {"dipak", "Deepak"}, {"deepaka", "Deepak"},
            {"amit", "Amit"}, {"amita", "Amit"}, {"ameet", "Amit"},
            {"rahul", "Rahul"}, {"raahul", "Rahul"}, {"rahuul", "Rahul"},
            {"rohit", "Rohit"}, {"roohit", "Rohit"}, {"rohita", "Rohit"},
            {"vishal", "Vishal"}, {"vishaal", "Vishal"}, {"vishala", "Vishal"},
            {"ajay", "Ajay"}, {"ajaya", "Ajay"}, {"ajai", "Ajay"},
            {"vijay", "Vijay"}, {"vijaya", "Vijay"}, {"vijai", "Vijay"},
            {"sanjay", "Sanjay"}, {"sanjaya", "Sanjay"}, {"sanjai", "Sanjay"},
            {"anil", "Anil"}, {"anila", "Anil"}, {"aneel", "Anil"},
            {"manoj", "Manoj"}, {"manoja", "Manoj"}, {"manooj", "Manoj"},
            {"ashok", "Ashok"}, {"ashoka", "Ashok"}, {"asok", "Ashok"},
            
            // Female Names
            {"priya", "Priya"}, {"priyaa", "Priya"}, {"preeya", "Priya"},
            {"pooja", "Pooja"}, {"puja", "Pooja"}, {"poojaa", "Pooja"},
            {"nandini", "Nandini"}, {"nandinii", "Nandini"}, {"nandani", "Nandini"},
            {"anushka", "Anushka"}, {"anushkaa", "Anushka"}, {"anuska", "Anushka"},
            {"shreya", "Shreya"}, {"shreyaa", "Shreya"}, {"shrayaa", "Shreya"},
            {"divya", "Divya"}, {"divyaa", "Divya"}, {"diviya", "Divya"},
            {"neha", "Neha"}, {"nehaa", "Neha"}, {"naeha", "Neha"},
            {"asha", "Asha"}, {"aashaa", "Asha"}, {"aasha", "Asha"},
            {"kavita", "Kavita"}, {"kavitaa", "Kavita"}, {"kaavita", "Kavita"},
            {"sunita", "Sunita"}, {"sunitaa", "Sunita"}, {"suneeta", "Sunita"},
            {"bhumika", "Bhumika"}, {"bhoomika", "Bhumika"}, {"bhuumikaa", "Bhumika"},
            {"gauri", "Gauri"}, {"gaaurii", "Gauri"}, {"gowri", "Gauri"}
        };

        private static readonly Dictionary<string, string> SurnameCorrections = new()
        {
            {"sharma", "Sharma"}, {"shaarmaa", "Sharma"}, {"sharman", "Sharma"},
            {"verma", "Verma"}, {"varma", "Verma"}, {"varmaa", "Verma"},
            {"kumar", "Kumar"}, {"kumara", "Kumar"}, {"kumaara", "Kumar"},
            {"singh", "Singh"}, {"singha", "Singh"}, {"simha", "Singh"},
            {"gupta", "Gupta"}, {"guptaa", "Gupta"}, {"gupt", "Gupta"},
            {"patel", "Patel"}, {"patela", "Patel"}, {"paatel", "Patel"},
            {"shah", "Shah"}, {"shaha", "Shah"}, {"shaah", "Shah"},
            {"jain", "Jain"}, {"jaina", "Jain"}, {"jaain", "Jain"},
            {"mehta", "Mehta"}, {"mehtaa", "Mehta"}, {"maehta", "Mehta"},
            {"agarwal", "Agarwal"}, {"agarwaal", "Agarwal"}, {"agrawaal", "Agarwal"},
            {"chowdhury", "Chowdhury"}, {"chaudhuri", "Chowdhury"}, {"choudhary", "Chowdhury"},
            {"reddy", "Reddy"}, {"reddii", "Reddy"}, {"readdy", "Reddy"},
            {"rao", "Rao"}, {"raao", "Rao"}, {"raav", "Rao"},
            {"kumawat", "Kumawat"}, {"kamawat", "Kumawat"}, {"kumaavat", "Kumawat"},
            {"tekam", "Tekam"}, {"tekaama", "Tekam"}, {"tekaam", "Tekam"},
            {"hada", "Hada"}, {"haadaa", "Hada"}, {"hadaa", "Hada"},
            {"dhara", "Dhara"}, {"dharaa", "Dhara"}, {"dhaara", "Dhara"},
            {"danot", "Danot"}, {"danotya", "Danot"}, {"danota", "Danot"}
        };

        public static string CorrectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            Console.WriteLine($"🔍 INDIAN NAME CORRECTOR INPUT: '{name}'");

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "";

            var correctedWords = new List<string>();

            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].ToLower().Trim();
                if (string.IsNullOrEmpty(word))
                    continue;

                // Try exact match first
                if (FirstNameCorrections.ContainsKey(word))
                {
                    correctedWords.Add(FirstNameCorrections[word]);
                    Console.WriteLine($"  ✓ First name corrected: '{word}' → '{FirstNameCorrections[word]}'");
                    continue;
                }

                if (SurnameCorrections.ContainsKey(word))
                {
                    correctedWords.Add(SurnameCorrections[word]);
                    Console.WriteLine($"  ✓ Surname corrected: '{word}' → '{SurnameCorrections[word]}'");
                    continue;
                }

                // Try fuzzy matching
                var fuzzyMatch = FindFuzzyMatch(word);
                if (!string.IsNullOrEmpty(fuzzyMatch))
                {
                    correctedWords.Add(fuzzyMatch);
                    Console.WriteLine($"  ✓ Fuzzy match: '{word}' → '{fuzzyMatch}'");
                    continue;
                }

                // No correction - capitalize as-is
                correctedWords.Add(CapitalizeWord(word));
            }

            var result = string.Join(" ", correctedWords);
            Console.WriteLine($"✅ INDIAN NAME CORRECTOR OUTPUT: '{result}'");
            
            return result;
        }

        private static string FindFuzzyMatch(string word)
        {
            if (word.Length < 3)
                return "";

            foreach (var kvp in FirstNameCorrections)
            {
                if (LevenshteinDistance(word, kvp.Key) <= 2)
                    return kvp.Value;
            }

            foreach (var kvp in SurnameCorrections)
            {
                if (LevenshteinDistance(word, kvp.Key) <= 2)
                    return kvp.Value;
            }

            return "";
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1))
                return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
            if (string.IsNullOrEmpty(s2))
                return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[s1.Length, s2.Length];
        }

        private static string CapitalizeWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "";
            return char.ToUpper(word[0]) + word.Substring(1).ToLower();
        }
    }
}