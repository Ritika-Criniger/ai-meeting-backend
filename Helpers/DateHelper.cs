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

            Console.WriteLine($"📅 RESOLVING DATE: '{input}'");

            // ==================================================
            // 🔥 NORMALIZE HINDI & ENGLISH VARIANTS
            // ==================================================
            input = NormalizeInput(input);

            // ==================================================
            // 🔥 TODAY / TOMORROW / DAY AFTER TOMORROW
            // ==================================================
            if (ContainsAny(input, "today", "aaj", "आज", "aj"))
            {
                Console.WriteLine($"✅ TODAY: {Format(today)}");
                return Format(today);
            }

            if (ContainsAny(input, "tomorrow", "kal", "कल"))
            {
                // Check if "next kal" or "agle kal" which means day after tomorrow
                if (ContainsAny(input, "next", "agle", "aagla", "आगले"))
                {
                    Console.WriteLine($"✅ DAY AFTER TOMORROW (next kal): {Format(today.AddDays(2))}");
                    return Format(today.AddDays(2));
                }
                Console.WriteLine($"✅ TOMORROW: {Format(today.AddDays(1))}");
                return Format(today.AddDays(1));
            }

            if (ContainsAny(input, "parso", "parson", "day after tomorrow", "परसों", "parsu"))
            {
                Console.WriteLine($"✅ DAY AFTER TOMORROW: {Format(today.AddDays(2))}");
                return Format(today.AddDays(2));
            }

            // ==================================================
            // 🔥 AFTER X DAYS (ENGLISH + HINDI) - ENHANCED
            // ==================================================
            var afterMatch = Regex.Match(
                input,
                @"(?:after|baad|बाद)\s+(\d+|one|two|three|four|five|six|seven|eight|nine|ten|ek|do|teen|char|panch|che|chhah|saat|aath|nau|das)\s+(?:day|days|din|दिन|दिनों)",
                RegexOptions.IgnoreCase
            );

            if (afterMatch.Success)
            {
                string numStr = afterMatch.Groups[1].Value.ToLower();
                int days = ConvertWordToNumber(numStr);

                if (days > 0)
                {
                    var result = Format(today.AddDays(days));
                    Console.WriteLine($"✅ AFTER {days} DAYS: {result}");
                    return result;
                }
            }

            // ==================================================
            // 🔥 SPECIFIC WEEKDAY (next monday, this friday, etc.) - FIXED
            // ==================================================
            var weekdayMatch = Regex.Match(
                input,
                @"(?:(next|this|coming|agle|agla|आगले|is)\s+)?(monday|tuesday|wednesday|thursday|friday|saturday|sunday|somwar|somvaar|mangal|mangalwar|mangalvaar|budh|budhwar|budhvaar|guru|guruwar|guruvaar|shukra|shukravar|shukravaar|shani|shaniwar|shanivaar|ravi|raviwar|ravivaar|सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार)",
                RegexOptions.IgnoreCase
            );

            if (weekdayMatch.Success)
            {
                string modifier = weekdayMatch.Groups[1].Value.ToLower();
                bool isNext = ContainsAny(modifier, "next", "agle", "agla", "आगले");
                bool isThis = ContainsAny(modifier, "this", "coming", "is");

                DayOfWeek targetDay = MapWeekday(weekdayMatch.Groups[2].Value);
                DateTime resolved = GetNextWeekday(today, targetDay, isNext, isThis);

                Console.WriteLine($"✅ WEEKDAY: {resolved.DayOfWeek} → {Format(resolved)}");
                return Format(resolved);
            }

            // ==================================================
            // 🔥 ABSOLUTE DATE WITH YEAR: "22 december 2025"
            // ==================================================
            var dateWithYearMatch = Regex.Match(
                input,
                @"(\d{1,2})\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(?:uary|ruary|ch|il|e|y|ust|tember|ober|ember)?\s+(\d{4})",
                RegexOptions.IgnoreCase
            );

            if (dateWithYearMatch.Success)
            {
                try
                {
                    int day = int.Parse(dateWithYearMatch.Groups[1].Value);
                    string monthStr = dateWithYearMatch.Groups[2].Value.ToLower();
                    int year = int.Parse(dateWithYearMatch.Groups[3].Value);

                    int month = GetMonthNumber(monthStr);

                    if (month > 0 && day >= 1 && day <= 31)
                    {
                        var date = new DateTime(year, month, day);
                        Console.WriteLine($"✅ ABSOLUTE DATE WITH YEAR: {Format(date)}");
                        return Format(date);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ INVALID DATE: {ex.Message}");
                }
            }

            // ==================================================
            // 🔥 ABSOLUTE DATE WITHOUT YEAR: "22 december"
            // ==================================================
            var dateNoYearMatch = Regex.Match(
                input,
                @"(\d{1,2})\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(?:uary|ruary|ch|il|e|y|ust|tember|ober|ember)?(?!\s+\d{4})",
                RegexOptions.IgnoreCase
            );

            if (dateNoYearMatch.Success)
            {
                try
                {
                    int day = int.Parse(dateNoYearMatch.Groups[1].Value);
                    string monthStr = dateNoYearMatch.Groups[2].Value.ToLower();

                    int month = GetMonthNumber(monthStr);

                    if (month > 0 && day >= 1 && day <= 31)
                    {
                        int year = today.Year;
                        var date = new DateTime(year, month, day);

                        // If date is in the past, assume next year
                        if (date < today)
                            date = date.AddYears(1);

                        Console.WriteLine($"✅ ABSOLUTE DATE WITHOUT YEAR: {Format(date)}");
                        return Format(date);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ INVALID DATE: {ex.Message}");
                }
            }

            // ==================================================
            // 🔥 NUMERIC DATES (DD/MM/YYYY, DD-MM-YYYY, etc.)
            // ==================================================
            var numericDateMatch = Regex.Match(
                input,
                @"(\d{1,2})[\s\-/.](\d{1,2})[\s\-/.](\d{2,4})"
            );

            if (numericDateMatch.Success)
            {
                try
                {
                    int day = int.Parse(numericDateMatch.Groups[1].Value);
                    int month = int.Parse(numericDateMatch.Groups[2].Value);
                    int year = int.Parse(numericDateMatch.Groups[3].Value);

                    if (year < 100)
                        year += 2000;

                    if (day >= 1 && day <= 31 && month >= 1 && month <= 12)
                    {
                        var date = new DateTime(year, month, day);
                        Console.WriteLine($"✅ NUMERIC DATE: {Format(date)}");
                        return Format(date);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ INVALID NUMERIC DATE: {ex.Message}");
                }
            }

            Console.WriteLine($"❌ COULD NOT RESOLVE DATE: '{input}'");
            return "";
        }

        // ==================================================
        // 🔥 HELPER: CONVERT WORD TO NUMBER - ENHANCED
        // ==================================================
        private static int ConvertWordToNumber(string word)
        {
            var wordMap = new Dictionary<string, int>
            {
                // English
                {"one", 1}, {"two", 2}, {"three", 3}, {"four", 4}, {"five", 5},
                {"six", 6}, {"seven", 7}, {"eight", 8}, {"nine", 9}, {"ten", 10},
                
                // Hindi
                {"ek", 1}, {"do", 2}, {"teen", 3}, {"char", 4}, {"panch", 5},
                {"che", 6}, {"chhah", 6}, {"saat", 7}, {"aath", 8}, {"nau", 9}, {"das", 10},
                
                // Numeric strings
                {"1", 1}, {"2", 2}, {"3", 3}, {"4", 4}, {"5", 5},
                {"6", 6}, {"7", 7}, {"8", 8}, {"9", 9}, {"10", 10}
            };

            return wordMap.ContainsKey(word) ? wordMap[word] : 0;
        }

        // ==================================================
        // 🔥 HELPER: GET MONTH NUMBER - ENHANCED
        // ==================================================
        private static int GetMonthNumber(string monthStr)
        {
            // Ensure minimum length
            if (monthStr.Length < 3)
                return 0;

            var monthMap = new Dictionary<string, int>
            {
                {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4},
                {"may", 5}, {"jun", 6}, {"jul", 7}, {"aug", 8},
                {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12}
            };

            monthStr = monthStr.Substring(0, Math.Min(3, monthStr.Length)).ToLower();
            return monthMap.ContainsKey(monthStr) ? monthMap[monthStr] : 0;
        }

        // ==================================================
        // 🔥 HELPER: NORMALIZE INPUT - ENHANCED
        // ==================================================
        private static string NormalizeInput(string input)
        {
            // Normalize month names
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

            // Hindi month names to English
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

        // ==================================================
        // 🔥 HELPER: GET NEXT WEEKDAY - FIXED LOGIC
        // ==================================================
        private static DateTime GetNextWeekday(DateTime start, DayOfWeek target, bool forceNext, bool allowSameWeek)
        {
            int daysToAdd = ((int)target - (int)start.DayOfWeek + 7) % 7;

            if (daysToAdd == 0)
            {
                // Today is the target day
                if (allowSameWeek)
                {
                    // "this Friday" when today is Friday → return today
                    return start;
                }
                else if (forceNext)
                {
                    // "next Friday" when today is Friday → 7 days later
                    return start.AddDays(7);
                }
                else
                {
                    // Ambiguous case → assume next week
                    return start.AddDays(7);
                }
            }
            else
            {
                // Target day is upcoming this week
                if (forceNext)
                {
                    // "next Friday" → skip this week's Friday, go to next
                    return start.AddDays(daysToAdd + 7);
                }
                else
                {
                    // "this Friday" or just "Friday" → upcoming occurrence
                    return start.AddDays(daysToAdd);
                }
            }
        }

        // ==================================================
        // 🔥 HELPER: MAP WEEKDAY - ENHANCED
        // ==================================================
        private static DayOfWeek MapWeekday(string input)
        {
            input = input.ToLower();

            return input switch
            {
                "monday" or "somwar" or "somvaar" or "सोमवार" => DayOfWeek.Monday,
                "tuesday" or "mangal" or "mangalwar" or "mangalvaar" or "मंगलवार" => DayOfWeek.Tuesday,
                "wednesday" or "budh" or "budhwar" or "budhvaar" or "बुधवार" => DayOfWeek.Wednesday,
                "thursday" or "guru" or "guruwar" or "guruvaar" or "गुरुवार" => DayOfWeek.Thursday,
                "friday" or "shukra" or "shukravar" or "shukravaar" or "शुक्रवार" => DayOfWeek.Friday,
                "saturday" or "shani" or "shaniwar" or "shanivaar" or "शनिवार" => DayOfWeek.Saturday,
                "sunday" or "ravi" or "raviwar" or "ravivaar" or "रविवार" => DayOfWeek.Sunday,
                _ => throw new ArgumentOutOfRangeException($"Unknown weekday: {input}")
            };
        }

        // ==================================================
        // 🔥 HELPER: FORMAT DATE
        // ==================================================
        private static string Format(DateTime date)
        {
            return date.ToString("dd-MM-yyyy");
        }

        // ==================================================
        // 🔥 HELPER: CONTAINS ANY
        // ==================================================
        private static bool ContainsAny(string text, params string[] words)
        {
            foreach (var w in words)
            {
                if (text.Contains(w, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ==================================================
        // 🔥 VALIDATION - ENHANCED
        // ==================================================
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

            // Date should be today or in the future, within 2 years
            return date >= DateTime.Today && date <= DateTime.Today.AddYears(2);
        }
    }
}