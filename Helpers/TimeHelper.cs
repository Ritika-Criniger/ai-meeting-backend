using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class TimeHelper
    {
        public static string Normalize(string time, string fullContext)
        {
            if (string.IsNullOrWhiteSpace(time))
                return "";

            time = time.Trim();
            string context = fullContext?.ToLower() ?? "";

            // ==================================================
            // PRIORITY 1: USER EXPLICITLY SAID AM/PM (HIGHEST PRIORITY)
            // ==================================================
            var explicitMatch = Regex.Match(time, @"(\d{1,2})(?::(\d{2}))?\s*(am|pm)", RegexOptions.IgnoreCase);
            if (explicitMatch.Success)
            {
                int hour = int.Parse(explicitMatch.Groups[1].Value);
                string minutes = explicitMatch.Groups[2].Success ? explicitMatch.Groups[2].Value : "00";
                string meridiem = explicitMatch.Groups[3].Value.ToUpper();

                // Convert 24h to 12h if needed
                if (hour > 12) hour = hour % 12;
                if (hour == 0) hour = 12;

                return $"{hour}:{minutes} {meridiem}";
            }

            // ==================================================
            // CHECK IF CONTEXT HAS AM/PM (SECONDARY PRIORITY)
            // ==================================================
            bool contextHasPM = Regex.IsMatch(context, @"\bpm\b", RegexOptions.IgnoreCase);
            bool contextHasAM = Regex.IsMatch(context, @"\bam\b", RegexOptions.IgnoreCase);

            // ==================================================
            // EXTRACT HOUR NUMBER
            // ==================================================
            var hourMatch = Regex.Match(time, @"\b(\d{1,2})\b");
            if (!hourMatch.Success)
                return "";

            int hourOnly = int.Parse(hourMatch.Groups[1].Value);

            // ==================================================
            // HANDLE 24-HOUR FORMAT (13-23)
            // ==================================================
            if (hourOnly >= 13 && hourOnly <= 23)
            {
                int hour12 = hourOnly - 12;
                return $"{hour12}:00 PM";
            }

            if (hourOnly == 0)
                return "12:00 AM";

            // ==================================================
            // APPLY CONTEXT AM/PM IF FOUND
            // ==================================================
            if (contextHasPM && !time.Contains("AM", StringComparison.OrdinalIgnoreCase))
            {
                return $"{hourOnly}:00 PM";
            }
            
            if (contextHasAM && !time.Contains("PM", StringComparison.OrdinalIgnoreCase))
            {
                return $"{hourOnly}:00 AM";
            }

            // ==================================================
            // SMART AM/PM INFERENCE (LAST RESORT)
            // ==================================================
            string inferredPeriod = InferPeriod(hourOnly, context);
            return $"{hourOnly}:00 {inferredPeriod}";
        }

        // ==================================================
        // 🧠 ENHANCED INTELLIGENT AM/PM LOGIC
        // ==================================================
        private static string InferPeriod(int hour, string context)
        {
            // ========== USER SAID MORNING ==========
            if (Has(context, "subah", "morning", "savere", "सुबह"))
            {
                if (hour >= 1 && hour <= 11)
                    return "AM";
            }

            // ========== USER SAID AFTERNOON ==========
            if (Has(context, "dopahar", "afternoon", "दोपहर", "noon", "lunch"))
            {
                if (hour == 12 || (hour >= 1 && hour <= 4))
                    return "PM";
            }

            // ========== USER SAID EVENING ==========
            if (Has(context, "shaam", "sham", "evening", "शाम"))
            {
                if (hour >= 5 && hour <= 9)
                    return "PM";
                if (hour >= 1 && hour <= 4)
                    return "PM"; // "shaam 3" = 3 PM
            }

            // ========== USER SAID NIGHT ==========
            if (Has(context, "raat", "night", "रात", "midnight"))
            {
                if (hour >= 1 && hour <= 5)
                    return "AM"; // Late night/early morning
                if (hour >= 9 && hour <= 11)
                    return "PM"; // Night time
                if (hour == 12)
                    return "AM"; // Midnight
            }

            // ========== NO CONTEXT - USE HOUR LOGIC ==========
            
            // 12 is noon by default (unless night context)
            if (hour == 12)
                return "PM";

            // 1-5 AM (unlikely for meetings unless explicitly night)
            if (hour >= 1 && hour <= 5)
                return "AM";

            // 6-8 could be morning or evening
            if (hour >= 6 && hour <= 8)
            {
                // Check for evening/after-work hints
                if (Has(context, "baad", "after", "office", "work", "evening"))
                    return "PM";
                return "AM"; // Default morning
            }

            // 9-11 typically morning meetings
            if (hour >= 9 && hour <= 11)
                return "AM";

            // Fallback to PM (most business meetings are afternoon)
            return "PM";
        }

        // ==================================================
        // TIME RANGE VALIDATION
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

                // Handle overnight meetings
                if (end <= start)
                    end = end.Value.AddDays(1);

                var duration = end.Value - start.Value;
                
                // Meeting should be 15 mins to 12 hours
                return duration.TotalMinutes >= 15 && duration.TotalHours <= 12;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // 🔥 ENHANCED PARSE TIME
        // ==================================================
        private static DateTime? ParseTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
                return null;

            try
            {
                // Try standard formats
                var formats = new[]
                {
                    "h:mm tt",      // 7:00 PM
                    "hh:mm tt",     // 07:00 PM
                    "h tt",         // 7 PM
                    "hh tt",        // 07 PM
                    "H:mm",         // 19:00 (24h)
                    "HH:mm",        // 07:00 (24h)
                    "h:mmtt",       // 7:00PM (no space)
                    "hh:mmtt"       // 07:00PM (no space)
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

                // Fallback to general parsing
                if (DateTime.TryParse(time, out var parsed))
                {
                    return parsed;
                }
            }
            catch { }

            return null;
        }

        // ==================================================
        // 🔥 CONVERT TO 24-HOUR FORMAT
        // ==================================================
        public static string To24HourFormat(string time12h)
        {
            var parsed = ParseTime(time12h);
            if (!parsed.HasValue)
                return "";

            return parsed.Value.ToString("HH:mm");
        }

        // ==================================================
        // 🔥 GET MEETING DURATION
        // ==================================================
        public static TimeSpan? GetDuration(string startTime, string endTime)
        {
            var start = ParseTime(startTime);
            var end = ParseTime(endTime);

            if (!start.HasValue || !end.HasValue)
                return null;

            var duration = end.Value - start.Value;
            
            // Handle overnight meetings
            if (duration.TotalMinutes < 0)
            {
                duration = duration.Add(TimeSpan.FromDays(1));
            }

            return duration;
        }

        // ==================================================
        // HELPER METHOD
        // ==================================================
        private static bool Has(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var keyword in keywords)
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            
            return false;
        }
    }
}