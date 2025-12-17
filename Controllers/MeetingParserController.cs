using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
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

        public MeetingParserController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Parse([FromBody] ParseRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Input text missing"
                });
            }

            var groqKey = _config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(groqKey))
            {
                return StatusCode(500, "Groq API key missing");
            }

            var userText = request.Text.Trim();

            // ===============================
            // 1️⃣ CALL GROQ FOR EXTRACTION
            // ===============================
            var aiResult = await CallGroqAsync(userText, groqKey);

            // ===============================
            // 2️⃣ SAFE FALLBACK (REGEX ONLY IF EMPTY)
            // ===============================
            ApplyRegexFallback(userText, aiResult);

            // ===============================
            // 3️⃣ NORMALIZATION (HELPERS)
            // ===============================

            // 🔹 NAME → Always Roman English
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                aiResult.ClientName =
                    HindiRomanTransliterator.ToRoman(aiResult.ClientName);
            }

            // 🔹 DATE
            if (!string.IsNullOrWhiteSpace(aiResult.MeetingDate))
            {
                aiResult.MeetingDate =
                    DateHelper.ResolveDate(aiResult.MeetingDate);
            }

            // 🔹 TIME
            if (!string.IsNullOrWhiteSpace(aiResult.StartTime))
            {
                aiResult.StartTime =
                    TimeHelper.Normalize(aiResult.StartTime, userText);
            }

            if (!string.IsNullOrWhiteSpace(aiResult.EndTime))
            {
                aiResult.EndTime =
                    TimeHelper.Normalize(aiResult.EndTime, userText);
            }

            // ===============================
            // 4️⃣ VALIDATION
            // ===============================
            var errors = Validate(aiResult);

            return Ok(new
            {
                success = errors.Count == 0,
                data = new
                {
                    clientName = aiResult.ClientName,
                    mobileNumber = aiResult.MobileNumber,
                    meetingDate = aiResult.MeetingDate,
                    startTime = aiResult.StartTime,
                    endTime = aiResult.EndTime
                },
                errors,
                confidence = aiResult.Confidence
            });
        }

        // ==================================================
        // 🔥 GROQ CALL (STRICT JSON)
        // ==================================================
        private async Task<AiResult> CallGroqAsync(string text, string apiKey)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = "llama-3.3-70b-versatile",
                temperature = 0,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content =
@"You are an information extraction AI for a CRM meeting scheduler.

Rules:
- Extract only what is explicitly present.
- Do NOT guess.
- Output valid JSON only.
- Name may be Hindi or English.
- Date as spoken phrase.
- Time as hour numbers only.
- No AM/PM.
- Empty string if not found."
                    },
                    new
                    {
                        role = "user",
                        content = text
                    }
                }
            };

            var response = await http.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AiResult();

            using var doc = JsonDocument.Parse(raw);
            var content =
                doc.RootElement
                   .GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString();

            return ParseAiJson(content);
        }

        // ==================================================
        // 🔥 AI JSON PARSE (SAFE)
        // ==================================================
        private AiResult ParseAiJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<AiResult>(json)
                       ?? new AiResult();
            }
            catch
            {
                return new AiResult();
            }
        }

        // ==================================================
        // 🔥 REGEX FALLBACK (ONLY IF EMPTY)
        // ==================================================
        private void ApplyRegexFallback(string text, AiResult result)
        {
            // Mobile
            if (string.IsNullOrWhiteSpace(result.MobileNumber))
            {
                var mobileMatch =
                    Regex.Match(text, @"\b[6-9]\d{9}\b");
                if (mobileMatch.Success)
                    result.MobileNumber = mobileMatch.Value;
            }

            // Time range
            if (string.IsNullOrWhiteSpace(result.StartTime) ||
                string.IsNullOrWhiteSpace(result.EndTime))
            {
                var timeMatch =
                    Regex.Match(text, @"(\d{1,2})\s*(se|to)\s*(\d{1,2})",
                        RegexOptions.IgnoreCase);

                if (timeMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                        result.StartTime = timeMatch.Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(result.EndTime))
                        result.EndTime = timeMatch.Groups[3].Value;
                }
            }
        }

        // ==================================================
        // 🔥 VALIDATION
        // ==================================================
        private List<string> Validate(AiResult r)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(r.ClientName))
                errors.Add("Client name missing");

            if (string.IsNullOrWhiteSpace(r.MobileNumber))
                errors.Add("Mobile number missing");

            if (string.IsNullOrWhiteSpace(r.MeetingDate))
                errors.Add("Meeting date missing");

            if (string.IsNullOrWhiteSpace(r.StartTime))
                errors.Add("Start time missing");

            if (string.IsNullOrWhiteSpace(r.EndTime))
                errors.Add("End time missing");

            return errors;
        }
    }

    // ==================================================
    // MODELS
    // ==================================================
    public class ParseRequest
    {
        public string Text { get; set; } = "";
    }

    public class AiResult
    {
        public string ClientName { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string MeetingDate { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public double Confidence { get; set; } = 0.9;
    }
}
