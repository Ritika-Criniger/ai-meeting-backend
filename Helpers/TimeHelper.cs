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
            // 🔥 PRIORITY 1: USER SAID AM / PM → TRUST IT FULLY
            // ==================================================
            var explicitMatch = Regex.Match(
                time,
                @"(\d{1,2})(?::(\d{2}))?\s*(am|pm)",
                RegexOptions.IgnoreCase
            );

            if (explicitMatch.Success)
            {
                int hour = int.Parse(explicitMatch.Groups[1].Value);
                string minutes = explicitMatch.Groups[2].Success ? explicitMatch.Groups[2].Value : "00";
                string period = explicitMatch.Groups[3].Value.ToUpper();

                if (hour == 0) hour = 12;
                if (hour > 12) hour = hour % 12;

                return $"{hour}:{minutes} {period}";
            }

            // ==================================================
            // 🔥 PRIORITY 2: EXTRACT HOUR (DO NOT CHANGE IT)
            // ==================================================
            var hourMatch = Regex.Match(time, @"\d{1,2}");
            if (!hourMatch.Success)
                return "";

            int hourOnly = int.Parse(hourMatch.Value);

            // ==================================================
            // 🔥 PRIORITY 3: CONTEXT-BASED AM / PM (ONLY DECISION)
            // ==================================================
            string periodFinal = DeterminePeriod(hourOnly, context);

            return $"{hourOnly}:00 {periodFinal}";
        }

        private static string DeterminePeriod(int hour, string context)
        {
            // --------- STRONG CONTEXT (OVERRIDES EVERYTHING) ----------
            if (ContainsAny(context, "subah", "morning", "savere", "सुबह"))
                return "AM";

            if (ContainsAny(context, "dopahar", "afternoon", "दोपहर"))
                return "PM";

            if (ContainsAny(context, "shaam", "sham", "evening", "शाम"))
                return "PM";

            if (ContainsAny(context, "raat", "night", "रात"))
                return "PM";

            // --------- 24-HOUR FORMAT ----------
            if (hour >= 13 && hour <= 23)
                return "PM";

            if (hour == 0)
                return "AM";

            if (hour == 12)
                return "PM";

            // ==================================================
            // 🔥 SAFE MEETING DEFAULT (LAST RESORT ONLY)
            // ==================================================
            // Only used when NO context words are present
            if (hour >= 7 && hour <= 11)
                return "PM";

            return "AM";
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var key in keywords)
                if (text.Contains(key))
                    return true;
            return false;
        }

        // --------- OPTIONAL VALIDATION ----------
        public static bool IsValidTimeRange(string startTime, string endTime)
        {
            try
            {
                var start = DateTime.ParseExact(startTime, "h:mm tt", CultureInfo.InvariantCulture);
                var end = DateTime.ParseExact(endTime, "h:mm tt", CultureInfo.InvariantCulture);

                if (end <= start)
                    end = end.AddDays(1);

                return end > start;
            }
            catch
            {
                return false;
            }
        }
    }
}
