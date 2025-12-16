using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class DateHelper
    {
        public static string ResolveDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            DateTime today = DateTime.Today;
            input = input.ToLower().Trim();

            // 🔥 SAFETY NET: Convert common Hindi date words to Roman
            input = input
                .Replace("अगले", "agle")
                .Replace("अगला", "agle")
                .Replace("आज", "aaj")
                .Replace("कल", "kal")
                .Replace("परसों", "parso")
                .Replace("सोमवार", "somwar")
                .Replace("सौंवार", "somwar")  // typo
                .Replace("मंगलवार", "mangal")
                .Replace("मंगल", "mangal")
                .Replace("बुधवार", "budh")
                .Replace("बुध", "budh")
                .Replace("गुरुवार", "guru")
                .Replace("गुरु", "guru")
                .Replace("शुक्रवार", "shukr")
                .Replace("शुक्र", "shukr")
                .Replace("शनिवार", "shani")
                .Replace("शनि", "shani")
                .Replace("रविवार", "ravi")
                .Replace("रवि", "ravi");

            // ---------------- REMOVE TIME WORDS ----------------
            input = Regex.Replace(
                input,
                @"\b(shaam|sham|subah|dopahar|raat|evening|morning|night|afternoon|pm|am|ko|को)\b",
                "",
                RegexOptions.IgnoreCase
            ).Trim();

            // 🔥 CHECK: Does input explicitly have "next/agle"?
            bool hasNextKeyword = Regex.IsMatch(
                input,
                @"\b(next|agle|अगले|अगला|नेक्स्ट)\b",
                RegexOptions.IgnoreCase
            );

            // ---------------- TODAY ----------------
            if (ContainsAny(input, "today", "aaj", "आज"))
                return Format(today);

            // ---------------- TOMORROW ----------------
            if (ContainsAny(input, "tomorrow"))
                return Format(today.AddDays(1));

            // ---------------- KAL (Tomorrow in Hindi) ----------------
            if (ContainsAny(input, "kal", "कल"))
            {
                return Format(today.AddDays(1));
            }

            // ---------------- DAY AFTER TOMORROW ----------------
            if (ContainsAny(input, "parso", "परसों", "day after tomorrow"))
                return Format(today.AddDays(2));

            // ---------------- AFTER X DAYS ----------------
            var daysMatch = Regex.Match(
                input,
                @"(?:after|baad)?\s*(\d+)\s*(day|days|din|दिन)",
                RegexOptions.IgnoreCase
            );

            if (daysMatch.Success)
            {
                int days = int.Parse(daysMatch.Groups[1].Value);
                return Format(today.AddDays(days));
            }

            // ---------------- NEXT WEEK ----------------
            if (ContainsAny(input, "next week", "agle hafte", "अगले हफ्ते"))
                return Format(today.AddDays(7));

            // ---------------- THIS WEEK ----------------
            if (ContainsAny(input, "this week", "is hafte", "इस हफ्ते"))
                return Format(today);

            // ---------------- DAY NAMES (English & Hindi) ----------------
            var daysOfWeek = new Dictionary<string, DayOfWeek>
            {
                // English
                { "monday", DayOfWeek.Monday }, 
                { "tuesday", DayOfWeek.Tuesday }, 
                { "wednesday", DayOfWeek.Wednesday }, 
                { "thursday", DayOfWeek.Thursday }, 
                { "friday", DayOfWeek.Friday }, 
                { "saturday", DayOfWeek.Saturday }, 
                { "sunday", DayOfWeek.Sunday },
                
                // Hindi (Roman)
                { "somwar", DayOfWeek.Monday }, 
                { "mangal", DayOfWeek.Tuesday }, { "mangalvar", DayOfWeek.Tuesday },
                { "budh", DayOfWeek.Wednesday }, { "budhwar", DayOfWeek.Wednesday },
                { "guru", DayOfWeek.Thursday }, { "guruwar", DayOfWeek.Thursday }, { "brihaspati", DayOfWeek.Thursday },
                { "shukr", DayOfWeek.Friday }, { "shukrawar", DayOfWeek.Friday },
                { "shani", DayOfWeek.Saturday }, { "shaniwar", DayOfWeek.Saturday },
                { "ravi", DayOfWeek.Sunday }, { "raviwar", DayOfWeek.Sunday }, { "itwar", DayOfWeek.Sunday },
                
                // Hindi (Devanagari) + Common Typos
                { "सोमवार", DayOfWeek.Monday }, { "सौंवार", DayOfWeek.Monday }, // common typo
                { "मंगल", DayOfWeek.Tuesday }, { "मंगलवार", DayOfWeek.Tuesday },
                { "बुध", DayOfWeek.Wednesday }, { "बुधवार", DayOfWeek.Wednesday },
                { "गुरु", DayOfWeek.Thursday }, { "गुरुवार", DayOfWeek.Thursday },
                { "शुक्र", DayOfWeek.Friday }, { "शुक्रवार", DayOfWeek.Friday },
                { "शनि", DayOfWeek.Saturday }, { "शनिवार", DayOfWeek.Saturday },
                { "रवि", DayOfWeek.Sunday }, { "रविवार", DayOfWeek.Sunday }
            };

            // 🔥 SMART LOGIC: "next/agle" के साथ weekday
            foreach (var day in daysOfWeek)
            {
                if (input.Contains(day.Key))
                {
                    int currentDay = (int)today.DayOfWeek;
                    int targetDay = (int)day.Value;
                    
                    // Calculate days until next occurrence
                    int daysUntil = ((targetDay - currentDay + 7) % 7);
                    
                    // If it's the same day today, go to next week
                    if (daysUntil == 0)
                        daysUntil = 7;
                    
                    // 🔥 KEY DECISION: 
                    // If "next/agle" is EXPLICITLY mentioned → add 7 days (skip this week)
                    // Otherwise → take upcoming occurrence (this week or next)
                    if (hasNextKeyword && daysUntil <= 7)
                    {
                        // User said "next friday" - they want NEXT week's friday, not this week
                        daysUntil += 7;
                    }
                    
                    return Format(today.AddDays(daysUntil));
                }
            }

            // ---------------- EXPLICIT DATE FORMATS ----------------
            // DD-MM-YYYY
            var dashDate = Regex.Match(input, @"\b(\d{2})-(\d{2})-(\d{4})\b");
            if (dashDate.Success)
            {
                try
                {
                    var date = new DateTime(
                        int.Parse(dashDate.Groups[3].Value), // Year
                        int.Parse(dashDate.Groups[2].Value), // Month
                        int.Parse(dashDate.Groups[1].Value)  // Day
                    );
                    return Format(date);
                }
                catch { }
            }

            // DD/MM/YYYY
            var slashDate = Regex.Match(input, @"\b(\d{2})/(\d{2})/(\d{4})\b");
            if (slashDate.Success)
            {
                try
                {
                    var date = new DateTime(
                        int.Parse(slashDate.Groups[3].Value), // Year
                        int.Parse(slashDate.Groups[2].Value), // Month
                        int.Parse(slashDate.Groups[1].Value)  // Day
                    );
                    return Format(date);
                }
                catch { }
            }

            return "";
        }

        private static string Format(DateTime date)
            => date.ToString("dd/MM/yyyy");

        private static bool ContainsAny(string input, params string[] keywords)
        {
            foreach (var word in keywords)
                if (input.Contains(word))
                    return true;
            return false;
        }
    }
}