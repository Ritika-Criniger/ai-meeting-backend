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
        // 🔥 FIXED GROQ CALL WITH BETTER PROMPT
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
                response_format = new { type = "json_object" },  // 🔥 FORCE JSON
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content =
@"You are a meeting information extractor. Extract ONLY the following fields from the user's text and return VALID JSON.

Fields to extract:
- clientName: Person's name (Hindi or English)
- mobileNumber: 10-digit phone number
- meetingDate: Date phrase as spoken (e.g., ""tomorrow"", ""22 December 2025"")
- startTime: Start time hour only (e.g., ""7"", ""10"")
- endTime: End time hour only (e.g., ""8"", ""11"")

Rules:
1. Extract ONLY what is explicitly mentioned
2. Do NOT guess or infer
3. Use empty string """" if not found
4. Return ONLY valid JSON, no other text
5. Keep numbers as strings

Example output:
{
  ""clientName"": ""Amit Verma"",
  ""mobileNumber"": ""6267388250"",
  ""meetingDate"": ""22 December 2025"",
  ""startTime"": ""7"",
  ""endTime"": ""8""
}"
                    },
                    new
                    {
                        role = "user",
                        content = text
                    }
                }
            };

            try
            {
                var response = await http.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"
                    )
                );

                var raw = await response.Content.ReadAsStringAsync();
                Console.WriteLine("🔥 RAW GROQ RESPONSE: " + raw);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ Groq API Error: " + raw);
                    return new AiResult();
                }

                using var doc = JsonDocument.Parse(raw);
                var content =
                    doc.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString();

                Console.WriteLine("📝 EXTRACTED CONTENT: " + content);

                return ParseAiJson(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ GROQ CALL ERROR: " + ex.Message);
                return new AiResult();
            }
        }

        // ==================================================
        // 🔥 IMPROVED JSON PARSER WITH BETTER ERROR HANDLING
        // ==================================================
        private AiResult ParseAiJson(string json)
        {
            try
            {
                // Remove any markdown code blocks if present
                json = json.Trim();
                if (json.StartsWith("```"))
                {
                    json = Regex.Replace(json, @"```json\s*", "");
                    json = Regex.Replace(json, @"```\s*$", "");
                    json = json.Trim();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<AiResult>(json, options);
                
                Console.WriteLine($"✅ PARSED: Name={result?.ClientName}, Mobile={result?.MobileNumber}, Date={result?.MeetingDate}, Start={result?.StartTime}, End={result?.EndTime}");
                
                return result ?? new AiResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ JSON PARSE ERROR: " + ex.Message);
                Console.WriteLine("❌ PROBLEMATIC JSON: " + json);
                return new AiResult();
            }
        }

        // ==================================================
        // 🔥 ENHANCED REGEX FALLBACK
        // ==================================================
        private void ApplyRegexFallback(string text, AiResult result)
        {
            Console.WriteLine("🔍 APPLYING REGEX FALLBACK...");

            // Mobile - look for 10-digit number starting with 6-9
            if (string.IsNullOrWhiteSpace(result.MobileNumber))
            {
                var mobileMatch = Regex.Match(text, @"\b[6-9]\d{9}\b");
                if (mobileMatch.Success)
                {
                    result.MobileNumber = mobileMatch.Value;
                    Console.WriteLine($"📱 REGEX FOUND MOBILE: {result.MobileNumber}");
                }
            }

            // Name - extract name before "meeting" or "के साथ"
            if (string.IsNullOrWhiteSpace(result.ClientName))
            {
                // Try Hindi pattern first
                var hindiMatch = Regex.Match(text, @"([\u0900-\u097F\s]+)\s+के\s+साथ", RegexOptions.IgnoreCase);
                if (hindiMatch.Success)
                {
                    result.ClientName = hindiMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (Hindi): {result.ClientName}");
                }
                else
                {
                    // Try English patterns
                    var englishMatch = Regex.Match(text, @"(?:meeting|meet)\s+(?:with\s+)?([A-Za-z\s]+?)(?:\s+(?:on|at|tomorrow|today|yesterday)|\s+\d)", RegexOptions.IgnoreCase);
                    if (englishMatch.Success)
                    {
                        result.ClientName = englishMatch.Groups[1].Value.Trim();
                        Console.WriteLine($"👤 REGEX FOUND NAME (English): {result.ClientName}");
                    }
                }
            }

            // Date - look for common patterns
            if (string.IsNullOrWhiteSpace(result.MeetingDate))
            {
                // Look for "tomorrow", "today", "kal", etc.
                if (Regex.IsMatch(text, @"\b(tomorrow|kal|कल)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "tomorrow";
                    Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                }
                else if (Regex.IsMatch(text, @"\b(today|aaj|आज)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "today";
                    Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                }
                else
                {
                    // Look for date patterns like "22 December 2025"
                    var dateMatch = Regex.Match(text, @"(\d{1,2})\s+(january|february|march|april|may|june|july|august|september|october|november|december|जनवरी|फरवरी|मार्च|अप्रैल|मई|जून|जुलाई|अगस्त|सितंबर|अक्टूबर|नवंबर|दिसंबर|दिसमबर)\s+(\d{4})", RegexOptions.IgnoreCase);
                    if (dateMatch.Success)
                    {
                        result.MeetingDate = dateMatch.Value;
                        Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                    }
                }
            }

            // Time range - look for patterns like "7 se 8", "10 pm to 11 pm", etc.
            if (string.IsNullOrWhiteSpace(result.StartTime) || string.IsNullOrWhiteSpace(result.EndTime))
            {
                // Pattern: "7 se 8", "10 to 11", "7 बजे से 8 बजे"
                var timeMatch = Regex.Match(text, @"(\d{1,2})\s*(?:बजे\s*)?(?:se|to|से)\s*(\d{1,2})", RegexOptions.IgnoreCase);
                
                if (timeMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        result.StartTime = timeMatch.Groups[1].Value;
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }

                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        result.EndTime = timeMatch.Groups[2].Value;
                        Console.WriteLine($"🕐 REGEX FOUND END TIME: {result.EndTime}");
                    }
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