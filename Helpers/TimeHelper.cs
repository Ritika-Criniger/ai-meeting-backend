using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class TimeHelper
    {
        // ==================================================
        // MAIN NORMALIZE FUNCTION - ENHANCED FOR "5.30" FORMAT
        // ==================================================
        public static string Normalize(string time, string fullContext)
        {
            if (string.IsNullOrWhiteSpace(time))
                return "";

            time = time.Trim();
            string context = fullContext?.ToLower() ?? "";

            Console.WriteLine($"🕐 NORMALIZING TIME: '{time}' (Context: '{context}')");

            // 🔥 FIX: Convert dot (.) to colon (:) for consistency
            // "5.30" → "5:30"
            time = Regex.Replace(time, @"(\d{1,2})\.(\d{2})", "$1:$2");
            Console.WriteLine($"🔄 After dot conversion: '{time}'");

            // =========================
            // 1️⃣ ALREADY HAS AM/PM
            // =========================
            if (Regex.IsMatch(time, @"(AM|PM|am|pm|पीएम|एएम)", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(time, @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM|am|pm|पीएम|एएम)", RegexOptions.IgnoreCase);
                
                if (match.Success)
                {
                    string hour = match.Groups[1].Value;
                    string minutes = match.Groups[2].Success ? match.Groups[2].Value : "00";
                    string meridiem = match.Groups[3].Value.ToUpper();
                    
                    // Normalize Hindi PM/AM
                    if (meridiem.Contains("पीएम"))
                        meridiem = "PM";
                    else if (meridiem.Contains("एएम"))
                        meridiem = "AM";
                    else
                        meridiem = meridiem.Replace("am", "AM").Replace("pm", "PM");

                    var result = $"{hour}:{minutes} {meridiem}";
                    Console.WriteLine($"✅ TIME WITH AM/PM: {result}");
                    return result;
                }
            }

            // =========================
            // 2️⃣ ENHANCED CONTEXT AM/PM DETECTION
            // =========================
            bool contextHasPM = Regex.IsMatch(context, @"\b(pm|पीएम|evening|shaam|sham|शाम|night|raat|रात|ko|को|baad|बाद)\b", RegexOptions.IgnoreCase);
            bool contextHasAM = Regex.IsMatch(context, @"\b(am|एएम|morning|subah|subhe|सुबह|savere|सवेरे)\b", RegexOptions.IgnoreCase);

            // =========================
            // 3️⃣ EXTRACT HOUR AND MINUTES
            // =========================
            var timeMatch = Regex.Match(time, @"(\d{1,2})(?::(\d{2}))?");
            
            if (!timeMatch.Success)
            {
                Console.WriteLine($"❌ COULD NOT PARSE TIME: '{time}'");
                return "";
            }

            int hourOnly = int.Parse(timeMatch.Groups[1].Value);
            string minutesPart = timeMatch.Groups[2].Success ? timeMatch.Groups[2].Value : "00";

            Console.WriteLine($"🔢 EXTRACTED: Hour={hourOnly}, Minutes={minutesPart}");

            // 🔥 VALIDATION: Minutes should be 0-59
            int minutesInt;
            if (!int.TryParse(minutesPart, out minutesInt) || minutesInt < 0 || minutesInt > 59)
            {
                Console.WriteLine($"❌ INVALID MINUTES: {minutesPart}");
                return "";
            }

            // =========================
            // 4️⃣ HANDLE 24-HOUR FORMAT
            // =========================
            if (hourOnly >= 13 && hourOnly <= 23)
            {
                hourOnly -= 12;
                var result = $"{hourOnly}:{minutesPart} PM";
                Console.WriteLine($"✅ 24H FORMAT: {result}");
                return result;
            }
            
            if (hourOnly == 0)
            {
                var result = $"12:{minutesPart} AM";
                Console.WriteLine($"✅ MIDNIGHT: {result}");
                return result;
            }

            // 🔥 VALIDATION: Hour should be 1-12
            if (hourOnly < 1 || hourOnly > 12)
            {
                Console.WriteLine($"❌ INVALID HOUR: {hourOnly}");
                return "";
            }

            // =========================
            // 5️⃣ APPLY CONTEXT AM/PM
            // =========================
            if (contextHasPM)
            {
                var result = $"{hourOnly}:{minutesPart} PM";
                Console.WriteLine($"✅ CONTEXT PM: {result}");
                return result;
            }
            
            if (contextHasAM)
            {
                var result = $"{hourOnly}:{minutesPart} AM";
                Console.WriteLine($"✅ CONTEXT AM: {result}");
                return result;
            }

            // =========================
            // 6️⃣ SMART INFERENCE
            // =========================
            string inferredPeriod = InferPeriod(hourOnly, minutesPart, context);
            var finalResult = $"{hourOnly}:{minutesPart} {inferredPeriod}";
            Console.WriteLine($"✅ INFERRED: {finalResult}");
            return finalResult;
        }

        // ==================================================
        // 🔥 SMART AM/PM INFERENCE - ENHANCED
        // ==================================================
        private static string InferPeriod(int hour, string minutes, string context)
        {
            Console.WriteLine($"🤔 INFERRING PERIOD: Hour={hour}, Minutes={minutes}");

            // ========== EXPLICIT TIME CONTEXT ==========
            
            // Morning indicators
            if (Has(context, "subah", "subhe", "morning", "savere", "suvere", "सुबह", "सवेरे", "dawn", "breakfast", "nashta", "नाश्ता"))
            {
                if (hour >= 5 && hour <= 11)
                {
                    Console.WriteLine("→ Morning context: AM");
                    return "AM";
                }
            }

            // Afternoon / daytime indicators
            if (Has(context, "dopahar", "afternoon", "दोपहर", "noon", "lunch", "दोपहर की", "दुपहर",
                     "din", "din me", "din mein", "daytime", "day time"))
            {
                if (hour == 12)
                {
                    Console.WriteLine("→ Noon context: PM");
                    return "PM";
                }
                if (hour >= 1 && hour <= 4)
                {
                    Console.WriteLine("→ Afternoon context: PM");
                    return "PM";
                }
            }

            // Evening indicators (ENHANCED - added "ko", "baad")
            if (Has(context, "shaam", "sham", "evening", "शाम", "tea", "chai", "ko", "को", "baad", "बाद"))
            {
                if (hour >= 4 && hour <= 11)
                {
                    Console.WriteLine("→ Evening context: PM");
                    return "PM";
                }
            }

            // Night indicators
            if (Has(context, "raat", "night", "रात", "midnight", "dinner", "late", "रात को", "रात में"))
            {
                if (hour >= 7 && hour <= 11)
                {
                    Console.WriteLine("→ Night context: PM");
                    return "PM";
                }
                if (hour >= 1 && hour <= 5)
                {
                    Console.WriteLine("→ Late night context: AM");
                    return "AM";
                }
                if (hour == 12)
                {
                    Console.WriteLine("→ Midnight context: AM");
                    return "AM";
                }
            }

            // ========== BUSINESS HOURS CONTEXT ==========
            
            // Office/work hours (typically PM)
            if (Has(context, "office", "work", "meeting", "conference", "call", "appointment", "कार्यालय", "मीटिंग"))
            {
                if (hour >= 2 && hour <= 7)
                {
                    Console.WriteLine("→ Business hours: PM");
                    return "PM";
                }
            }

            // ========== DEFAULT LOGIC BY HOUR ==========
            
            // 12 - Always assume noon (PM) unless explicitly night
            if (hour == 12)
            {
                Console.WriteLine("→ Default 12: PM (noon)");
                return "PM";
            }

            // 1-5 - Could be early morning or afternoon/evening
            if (hour >= 1 && hour <= 5)
            {
                // If any night/late words, assume AM (late night/early morning)
                if (Has(context, "night", "raat", "late", "early", "dawn", "रात", "देर"))
                {
                    Console.WriteLine("→ Default 1-5 with night context: AM");
                    return "AM";
                }
                // Otherwise, likely afternoon/evening
                Console.WriteLine("→ Default 1-5: PM");
                return "PM";
            }

            // 6-8 - Could be morning or evening
            if (hour >= 6 && hour <= 8)
            {
                // Check for evening/office indicators
                if (Has(context, "evening", "shaam", "after", "office", "work", "tea", "dinner", "baad", "को"))
                {
                    Console.WriteLine("→ Default 6-8 with evening context: PM");
                    return "PM";
                }
                // Default to morning
                Console.WriteLine("→ Default 6-8: AM");
                return "AM";
            }

            // 9-11 - Usually morning unless explicitly evening/night
            if (hour >= 9 && hour <= 11)
            {
                // Unless explicitly evening/night
                if (Has(context, "evening", "night", "shaam", "raat", "dinner", "late", "को", "baad"))
                {
                    Console.WriteLine("→ Default 9-11 with evening context: PM");
                    return "PM";
                }
                Console.WriteLine("→ Default 9-11: AM");
                return "AM";
            }

            // Fallback
            Console.WriteLine("→ Fallback: PM");
            return "PM";
        }

        // ==================================================
        // TIME RANGE VALIDATION - ENHANCED
        // ==================================================
        public static bool IsValidTimeRange(string startTime, string endTime)
        {
            if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
                return false;

            try
            {
                var start = ParseTime(startTime);
                var end = ParseTime(endTime);

                if (!start.HasValue || !end.HasValue)
                    return false;

                // If end is before start, assume it's next day (overnight meeting)
                if (end <= start)
                    end = end.Value.AddDays(1);

                var duration = end.Value - start.Value;
                
                // Meeting should be between 15 minutes and 24 hours
                return duration.TotalMinutes >= 15 && duration.TotalHours <= 24;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // PARSE TIME STRING TO DATETIME - ENHANCED
        // ==================================================
        private static DateTime? ParseTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time)) 
                return null;

            try
            {
                // 🔥 FIX: Convert dot to colon before parsing
                time = Regex.Replace(time, @"(\d{1,2})\.(\d{2})", "$1:$2");

                var formats = new[]
                {
                    "h:mm tt", "hh:mm tt", "h tt", "hh tt", 
                    "H:mm", "HH:mm", "h:mmtt", "hh:mmtt",
                    "h:mm", "hh:mm", "h.mm tt", "hh.mm tt"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(
                        time.Trim(),
                        format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var result))
                    {
                        return result;
                    }
                }

                // Last resort: try general parse
                if (DateTime.TryParse(time, out var parsed)) 
                    return parsed;
            }
            catch { }

            return null;
        }

        // ==================================================
        // 24-HOUR FORMAT CONVERSION
        // ==================================================
        public static string To24HourFormat(string time12h)
        {
            var parsed = ParseTime(time12h);
            if (!parsed.HasValue) 
                return "";
            
            return parsed.Value.ToString("HH:mm");
        }

        // ==================================================
        // MEETING DURATION - ENHANCED FOR OVERNIGHT
        // ==================================================
        public static TimeSpan? GetDuration(string startTime, string endTime)
        {
            var start = ParseTime(startTime);
            var end = ParseTime(endTime);

            if (!start.HasValue || !end.HasValue) 
                return null;

            var duration = end.Value - start.Value;
            
            // If negative, assume next day (overnight meeting)
            if (duration.TotalMinutes < 0) 
                duration = duration.Add(TimeSpan.FromDays(1));

            return duration;
        }

        // ==================================================
        // HELPER FOR KEYWORDS
        // ==================================================
        private static bool Has(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return false;

            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}