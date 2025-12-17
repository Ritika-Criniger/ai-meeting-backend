using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Helpers
{
    public static class TimeHelper
    {
        // ==================================================
        // MAIN NORMALIZE FUNCTION
        // ==================================================
        public static string Normalize(string time, string fullContext)
        {
            if (string.IsNullOrWhiteSpace(time))
                return "";

            time = time.Trim();
            string context = fullContext?.ToLower() ?? "";

            // =========================
            // 1️⃣ CHECK IF TIME ALREADY HAS AM/PM
            // =========================
            if (Regex.IsMatch(time, @"\d{1,2}(:\d{2})?\s*(AM|PM)", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(time, @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM)", RegexOptions.IgnoreCase);
                string hour = match.Groups[1].Value;
                string minutes = match.Groups[2].Success ? match.Groups[2].Value : "00";
                string meridiem = match.Groups[3].Value.ToUpper();
                return $"{hour}:{minutes} {meridiem}";
            }

            // =========================
            // 2️⃣ CONTEXT AM/PM DETECTION
            // =========================
            bool contextHasPM = Regex.IsMatch(context, @"\bpm\b", RegexOptions.IgnoreCase);
            bool contextHasAM = Regex.IsMatch(context, @"\bam\b", RegexOptions.IgnoreCase);

            // =========================
            // 3️⃣ EXTRACT HOUR AND MINUTES
            // =========================
            var hourMatch = Regex.Match(time, @"(\d{1,2})(?::(\d{2}))?");
            if (!hourMatch.Success)
                return "";

            int hourOnly = int.Parse(hourMatch.Groups[1].Value);
            string minutesPart = hourMatch.Groups[2].Success ? hourMatch.Groups[2].Value : "00";

            // =========================
            // 4️⃣ HANDLE 24H FORMAT
            // =========================
            if (hourOnly >= 13 && hourOnly <= 23)
            {
                hourOnly -= 12;
                return $"{hourOnly}:{minutesPart} PM";
            }
            if (hourOnly == 0)
                return $"12:{minutesPart} AM";

            // =========================
            // 5️⃣ APPLY CONTEXT AM/PM
            // =========================
            if (contextHasPM)
                return $"{hourOnly}:{minutesPart} PM";
            if (contextHasAM)
                return $"{hourOnly}:{minutesPart} AM";

            // =========================
            // 6️⃣ SMART INFERENCE (ONLY IF MINUTES=00)
            // =========================
            string inferredPeriod = "AM";
            if (minutesPart == "00")
                inferredPeriod = InferPeriod(hourOnly, context);

            return $"{hourOnly}:{minutesPart} {inferredPeriod}";
        }

        // ==================================================
        // SMART AM/PM INFERENCE
        // ==================================================
        private static string InferPeriod(int hour, string context)
        {
            // ========== MORNING ==========
            if (Has(context, "subah", "morning", "savere", "सुबह"))
            {
                if (hour >= 1 && hour <= 11) return "AM";
            }

            // ========== AFTERNOON ==========
            if (Has(context, "dopahar", "afternoon", "दोपहर", "noon", "lunch"))
            {
                if (hour == 12 || (hour >= 1 && hour <= 4)) return "PM";
            }

            // ========== EVENING ==========
            if (Has(context, "shaam", "sham", "evening", "शाम"))
            {
                if (hour >= 5 && hour <= 9) return "PM";
                if (hour >= 1 && hour <= 4) return "PM";
            }

            // ========== NIGHT ==========
            if (Has(context, "raat", "night", "रात", "midnight"))
            {
                if (hour >= 1 && hour <= 5) return "AM";
                if (hour >= 9 && hour <= 11) return "PM";
                if (hour == 12) return "AM";
            }

            // DEFAULT LOGIC
            if (hour == 12) return "PM";
            if (hour >= 1 && hour <= 5) return "AM";
            if (hour >= 6 && hour <= 8)
            {
                if (Has(context, "baad", "after", "office", "work", "evening")) return "PM";
                return "AM";
            }
            if (hour >= 9 && hour <= 11) return "AM";

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

                if (end <= start)
                    end = end.Value.AddDays(1);

                var duration = end.Value - start.Value;
                return duration.TotalMinutes >= 15 && duration.TotalHours <= 12;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // PARSE TIME STRING TO DATETIME
        // ==================================================
        private static DateTime? ParseTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time)) return null;

            try
            {
                var formats = new[]
                {
                    "h:mm tt", "hh:mm tt", "h tt", "hh tt", "H:mm", "HH:mm", "h:mmtt", "hh:mmtt"
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

                if (DateTime.TryParse(time, out var parsed)) return parsed;
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
            if (!parsed.HasValue) return "";
            return parsed.Value.ToString("HH:mm");
        }

        // ==================================================
        // MEETING DURATION
        // ==================================================
        public static TimeSpan? GetDuration(string startTime, string endTime)
        {
            var start = ParseTime(startTime);
            var end = ParseTime(endTime);

            if (!start.HasValue || !end.HasValue) return null;

            var duration = end.Value - start.Value;
            if (duration.TotalMinutes < 0) duration = duration.Add(TimeSpan.FromDays(1));

            return duration;
        }

        // ==================================================
        // HELPER FOR KEYWORDS
        // ==================================================
        private static bool Has(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (var keyword in keywords)
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }
    }
}
