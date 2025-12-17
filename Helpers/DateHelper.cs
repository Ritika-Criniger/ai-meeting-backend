using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class DateHelper
    {
        public static string ResolveDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            input = input.Trim().ToLower();
            DateTime today = DateTime.Today;

            // ==================================================
            // 🔥 NORMALIZE HINDI & ENGLISH MONTH NAMES
            // ==================================================
            input = NormalizeMonths(input);

            // ==================================================
            // 🔥 TODAY / TOMORROW / DAY AFTER TOMORROW
            // ==================================================
            if (ContainsAny(input, "aaj", "today", "आज"))
                return Format(today);

            if (ContainsAny(input, "kal", "tomorrow", "कल"))
                return Format(today.AddDays(1));

            if (ContainsAny(input, "parso", "parson", "day after tomorrow", "परसों"))
                return Format(today.AddDays(2));

            // ==================================================
            // 🔥 AFTER X DAYS (ENGLISH + HINDI MIXED)
            // ==================================================
            var afterMatch = Regex.Match(
                input,
                @"(after|आफ्टर|baad|बाद)\s+(one|two|three|four|five|six|seven|1|2|3|4|5|6|7|एक|दो|तीन|टू|ون)\s+(day|days|din|दिन|डेज)",
                RegexOptions.IgnoreCase
            );

            if (afterMatch.Success)
            {
                string numStr = afterMatch.Groups[2].Value.ToLower();
                int days = ConvertWordToNumber(numStr);
                
                if (days > 0)
                {
                    Console.WriteLine($"📅 RESOLVED 'after {days} days' → {Format(today.AddDays(days))}");
                    return Format(today.AddDays(days));
                }
            }

            // ==================================================
            // 🔥 ABSOLUTE DATE: "22 December 2025" or "22 Dec 2025"
            // ==================================================
            var absoluteDateMatch = Regex.Match(
                input,
                @"(\d{1,2})\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\w*\s+(\d{4})",
                RegexOptions.IgnoreCase
            );

            if (absoluteDateMatch.Success)
            {
                int day = int.Parse(absoluteDateMatch.Groups[1].Value);
                string monthStr = absoluteDateMatch.Groups[2].Value.ToLower();
                int year = int.Parse(absoluteDateMatch.Groups[3].Value);
                
                var monthMap = new Dictionary<string, int>
                {
                    {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4},
                    {"may", 5}, {"jun", 6}, {"jul", 7}, {"aug", 8},
                    {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12}
                };
                
                if (monthMap.ContainsKey(monthStr))
                {
                    try
                    {
                        var date = new DateTime(year, monthMap[monthStr], day);
                        return Format(date);
                    }
                    catch
                    {
                        // Invalid date (e.g., 31 Feb)
                    }
                }
            }

            // ==================================================
            // 🔥 ABSOLUTE DATE WITHOUT YEAR: "22 December"
            // ==================================================
            var dateNoYearMatch = Regex.Match(
                input,
                @"(\d{1,2})\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\w*(?!\s+\d{4})",
                RegexOptions.IgnoreCase
            );

            if (dateNoYearMatch.Success)
            {
                int day = int.Parse(dateNoYearMatch.Groups[1].Value);
                string monthStr = dateNoYearMatch.Groups[2].Value.ToLower();
                
                var monthMap = new Dictionary<string, int>
                {
                    {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4},
                    {"may", 5}, {"jun", 6}, {"jul", 7}, {"aug", 8},
                    {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12}
                };
                
                if (monthMap.ContainsKey(monthStr))
                {
                    try
                    {
                        int year = today.Year;
                        var date = new DateTime(year, monthMap[monthStr], day);
                        
                        if (date < today)
                            date = date.AddYears(1);
                        
                        return Format(date);
                    }
                    catch { }
                }
            }

            // ==================================================
            // 🔥 WEEKDAY LOGIC
            // ==================================================
            var weekdayMatch = Regex.Match(
                input,
                @"(next|this|coming|agle|agla|आगले)?\s*(monday|tuesday|wednesday|thursday|friday|saturday|sunday|somwar|mangal|mangalwar|budh|budhwar|guru|guruwar|shukra|shukravar|shani|shaniwar|ravi|raviwar|सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार)",
                RegexOptions.IgnoreCase
            );

            if (weekdayMatch.Success)
            {
                bool isNext = ContainsAny(input, "next", "agle", "agla", "आगले");
                bool isThis = input.Contains("this");
                bool isComing = input.Contains("coming");

                DayOfWeek targetDay = MapWeekday(weekdayMatch.Groups[2].Value);
                DateTime resolved = GetNextWeekday(today, targetDay, isNext, isThis || isComing);

                return Format(resolved);
            }

            // ==================================================
            // 🔥 NUMERIC DATES
            // ==================================================
            if (DateTime.TryParseExact(
                input,
                new[]
                {
                    "d/M/yyyy", "dd/MM/yyyy",
                    "d-M-yyyy", "dd-MM-yyyy",
                    "d.M.yyyy", "dd.MM.yyyy",
                    "d/M/yy", "dd/MM/yy",
                    "d-M-yy", "dd-MM-yy"
                },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var numericDate))
            {
                if (numericDate.Year < 100)
                    numericDate = numericDate.AddYears(2000);

                return Format(numericDate);
            }

            return "";
        }

        // ==================================================
        // 🔥 NEW: CONVERT WORD TO NUMBER
        // ==================================================
        private static int ConvertWordToNumber(string word)
        {
            var wordMap = new Dictionary<string, int>
            {
                {"one", 1}, {"two", 2}, {"three", 3}, {"four", 4},
                {"five", 5}, {"six", 6}, {"seven", 7},
                {"1", 1}, {"2", 2}, {"3", 3}, {"4", 4},
                {"5", 5}, {"6", 6}, {"7", 7},
                {"एक", 1}, {"दो", 2}, {"तीन", 3}, {"टू", 2}, {"ون", 1}
            };

            return wordMap.ContainsKey(word) ? wordMap[word] : 0;
        }

        // ==================================================
        // HELPER METHODS
        // ==================================================
        private static string NormalizeMonths(string input)
        {
            input = Regex.Replace(input, @"\bjanuary\b", "jan", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bfebruary\b", "feb", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bmarch\b", "mar", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bapril\b", "apr", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bjune\b", "jun", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bjuly\b", "jul", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\baugust\b", "aug", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bseptember\b", "sep", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\boctober\b", "oct", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bnovember\b", "nov", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\bdecember\b", "dec", RegexOptions.IgnoreCase);

            input = input.Replace("जनवरी", "jan");
            input = input.Replace("फ़रवरी", "feb");
            input = input.Replace("फरवरी", "feb");
            input = input.Replace("मार्च", "mar");
            input = input.Replace("अप्रैल", "apr");
            input = input.Replace("मई", "may");
            input = input.Replace("जून", "jun");
            input = input.Replace("जुलाई", "jul");
            input = input.Replace("अगस्त", "aug");
            input = input.Replace("सितंबर", "sep");
            input = input.Replace("अक्टूबर", "oct");
            input = input.Replace("नवंबर", "nov");
            input = input.Replace("दिसंबर", "dec");
            input = input.Replace("दिसमबर", "dec");

            return input;
        }

        private static DateTime GetNextWeekday(DateTime start, DayOfWeek target, bool forceNext, bool allowSameWeek)
        {
            int daysToAdd = ((int)target - (int)start.DayOfWeek + 7) % 7;

            if (daysToAdd == 0)
            {
                if (!allowSameWeek)
                    daysToAdd = 7;
            }

            if (forceNext && daysToAdd < 7)
                daysToAdd += 7;

            return start.AddDays(daysToAdd);
        }

        private static DayOfWeek MapWeekday(string input)
        {
            input = input.ToLower();
            
            return input switch
            {
                "somwar" or "monday" or "सोमवार" => DayOfWeek.Monday,
                "mangal" or "mangalwar" or "tuesday" or "मंगलवार" => DayOfWeek.Tuesday,
                "budh" or "budhwar" or "wednesday" or "बुधवार" => DayOfWeek.Wednesday,
                "guru" or "guruwar" or "thursday" or "गुरुवार" => DayOfWeek.Thursday,
                "shukra" or "shukravar" or "friday" or "शुक्रवार" => DayOfWeek.Friday,
                "shani" or "shaniwar" or "saturday" or "शनिवार" => DayOfWeek.Saturday,
                "ravi" or "raviwar" or "sunday" or "रविवार" => DayOfWeek.Sunday,
                _ => throw new ArgumentOutOfRangeException($"Unknown weekday: {input}")
            };
        }

        private static string Format(DateTime date)
        {
            return date.ToString("dd-MM-yyyy");
        }

        private static bool ContainsAny(string text, params string[] words)
        {
            foreach (var w in words)
            {
                if (text.Contains(w, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool IsValidDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return false;

            if (!DateTime.TryParseExact(
                dateStr,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                return false;
            }

            return date >= DateTime.Today && date <= DateTime.Today.AddYears(1);
        }
    }
}