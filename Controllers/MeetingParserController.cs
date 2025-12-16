using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiMeetingBackend.Helpers;

namespace AiMeetingBackend.Controllers
{
    [ApiController]
    [Route("api/parse-meeting")]
    public class MeetingParserController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MeetingParserController> _logger;

        public MeetingParserController(
            IConfiguration config,
            ILogger<MeetingParserController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ParseMeeting([FromBody] MeetingRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { success = false, error = "Text is required" });

            _logger.LogInformation($"📝 Parsing text: {request.Text}");

            MeetingParseResult result = new();
            bool aiSucceeded = false;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                result = await ExtractWithAI(request.Text, cts.Token);
                aiSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ AI failed: {ex.Message}");
            }

            // 🔥 Regex fallback if AI failed OR data is suspicious
            result = ApplyRegexFallback(request.Text, result);

            // 🔥 Final normalization
            result = ProcessDatesAndTimes(result, request.Text);

            var validation = ValidateResult(result);

            return Ok(new
            {
                success = validation.isValid,
                data = result,
                errors = validation.errors
            });
        }

        // ================= AI EXTRACTION =================
        private async Task<MeetingParseResult> ExtractWithAI(string text, CancellationToken ct)
        {
            var apiKey = _config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Groq API key missing");

            var today = DateTime.Today.ToString("dddd, dd MMMM yyyy");

            var prompt = $@"
You are a meeting scheduler AI. Extract details from Hindi/English/Hinglish text.

Current Date: {today}

CRITICAL RULES:
1. DATE EXTRACTION:
   - PRESERVE ""next/agle"" keyword with day names
   - If you see Hindi: ""अगले सोमवार"" → output ""agle somwar""
   - If you see: ""नेक्स्ट फ्राइडे"" → output ""next friday""
   - If you see: ""कल"" → output ""kal""
   - If you see: ""परसों"" → output ""parso""
   - If you see: ""आज"" → output ""aaj""
   - Convert Hindi weekday names to Roman: सोमवार→somwar, मंगलवार→mangal, बुधवार→budh, गुरुवार→guru, शुक्रवार→friday, शनिवार→shani, रविवार→ravi
   - Keep phrases like ""3 din baad"", ""after 2 days"" as-is
   - DO NOT calculate exact dates - keep it as phrase

2. TIME EXTRACTION:
   - Extract hour numbers: ""7 बजे"" → ""7""
   - If range: ""7 से 8"" → startTime=""7"", endTime=""8""
   - Don't add AM/PM (backend will handle)

3. NAME EXTRACTION:
   - Keep names EXACTLY as spoken (Hindi or English)
   - ""संदीप कारपेंटर"" → ""संदीप कारपेंटर"" (unchanged)
   - ""Rohit Kumar"" → ""Rohit Kumar"" (unchanged)

4. MOBILE:
   - Extract 10-digit numbers starting with 6-9

5. OUTPUT FORMAT:
   - Return ONLY valid JSON, no explanation
   - Empty string if field not found

JSON Schema:
{{
  ""clientName"": """",
  ""mobileNumber"": """",
  ""meetingDate"": """",
  ""startTime"": """",
  ""endTime"": """"
}}

Text to parse:
{text}
";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.1,
                max_tokens = 200
            };

            var res = await http.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct
            );

            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new Exception(raw);

            var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var json = content!.Substring( content.IndexOf('{'),
                                           content.LastIndexOf('}') - content.IndexOf('{') + 1);

            return JsonSerializer.Deserialize<MeetingParseResult>(json) ?? new MeetingParseResult();
        }

        // ================= REGEX FALLBACK (WITH HINDI SUPPORT) =================
        private MeetingParseResult ApplyRegexFallback(string text, MeetingParseResult result)
        {
            // ---------- Mobile ----------
            if (string.IsNullOrWhiteSpace(result.mobileNumber))
            {
                var m = Regex.Match(text, @"\b[6-9]\d{9}\b");
                if (m.Success) result.mobileNumber = m.Value;
            }

            // ---------- Name (🔥 HINDI SUPPORT ADDED) ----------
            if (string.IsNullOrWhiteSpace(result.clientName))
            {
                // 🔥 NEW: Support both English and Hindi characters
                var nameMatch = Regex.Match(
                    text,
                    @"(?:with|ke\s+sath|के\s+साथ)\s+([\p{L}\s]{2,30}?)(?=\s+(?:\d|se|to|kal|parso|aaj|meeting|मीटिंग|shaam|subah|mobile|phone|\d{10}))",
                    RegexOptions.IgnoreCase);

                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value.Trim();
                    // Remove trailing prepositions
                    name = Regex.Replace(name, @"\s+(se|sa|ke|ko|ka)$", "", RegexOptions.IgnoreCase);
                    result.clientName = name;
                }
            }

            // ---------- Date ----------
            if (string.IsNullOrWhiteSpace(result.meetingDate))
            {
                if (Regex.IsMatch(text, @"kal|tomorrow|कल", RegexOptions.IgnoreCase))
                    result.meetingDate = "kal";
                else if (Regex.IsMatch(text, @"parso|day after tomorrow|परसों", RegexOptions.IgnoreCase))
                    result.meetingDate = "parso";
                else if (Regex.IsMatch(text, @"(next|agle|अगले)\s+(somwar|monday|mangal|tuesday|budh|wednesday|guru|thursday|shukr|friday|shani|saturday|ravi|sunday|सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार)", RegexOptions.IgnoreCase))
                {
                    var dayMatch = Regex.Match(text, @"(next|agle|अगले)\s+(somwar|monday|mangal|tuesday|budh|wednesday|guru|thursday|shukr|friday|shani|saturday|ravi|sunday|सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार)", RegexOptions.IgnoreCase);
                    result.meetingDate = dayMatch.Value.ToLower();
                }
            }

            // ---------- TIME (🔥 MAIN FIX) ----------
            bool hasRangeWords = Regex.IsMatch(text, @"\b(se|to|tak|-)\b", RegexOptions.IgnoreCase);

            bool aiTimeInvalid =
                !string.IsNullOrWhiteSpace(result.startTime) &&
                !string.IsNullOrWhiteSpace(result.endTime) &&
                result.startTime == result.endTime;

            if (
                (string.IsNullOrWhiteSpace(result.startTime) &&
                 string.IsNullOrWhiteSpace(result.endTime))
                ||
                (hasRangeWords && aiTimeInvalid)
            )
            {
                var rangeMatch = Regex.Match(
                    text,
                    @"\b(\d{1,2})\s*(?:to|se|tak|-)\s*(\d{1,2})\b",
                    RegexOptions.IgnoreCase
                );

                if (rangeMatch.Success)
                {
                    result.startTime = rangeMatch.Groups[1].Value;
                    result.endTime   = rangeMatch.Groups[2].Value;
                }
            }

            return result;
        }

        // ================= FINAL NORMALIZATION =================
        private MeetingParseResult ProcessDatesAndTimes(MeetingParseResult result, string fullText)
        {
            if (!string.IsNullOrWhiteSpace(result.meetingDate))
            {
                var resolved = DateHelper.ResolveDate(result.meetingDate);
                if (!string.IsNullOrWhiteSpace(resolved))
                    result.meetingDate = resolved;
            }

            if (!string.IsNullOrWhiteSpace(result.startTime))
                result.startTime = TimeHelper.Normalize(result.startTime, fullText);

            if (!string.IsNullOrWhiteSpace(result.endTime))
                result.endTime = TimeHelper.Normalize(result.endTime, fullText);

            // 🔥 OPTIONAL: Hindi → Roman (only if you want transliteration)
            // Comment this out if you want to keep Hindi names as-is
            /*
            if (!string.IsNullOrWhiteSpace(result.clientName) &&
                HindiRomanTransliterator.IsHindi(result.clientName))
            {
                result.clientName = HindiRomanTransliterator.ToRoman(result.clientName);
            }
            */

            return result;
        }

        // ================= VALIDATION =================
        private (bool isValid, List<string> errors) ValidateResult(MeetingParseResult result)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(result.meetingDate))
                errors.Add("Meeting date missing");

            if (string.IsNullOrWhiteSpace(result.startTime))
                errors.Add("Start time missing");

            if (string.IsNullOrWhiteSpace(result.endTime))
                errors.Add("End time missing");

            return (errors.Count == 0, errors);
        }
    }

    // ================= MODELS =================
    public class MeetingRequest
    {
        public string Text { get; set; } = "";
    }

    public class MeetingParseResult
    {
        public string clientName { get; set; } = "";
        public string mobileNumber { get; set; } = "";
        public string meetingDate { get; set; } = "";
        public string startTime { get; set; } = "";
        public string endTime { get; set; } = "";
    }
}