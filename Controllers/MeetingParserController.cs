// MeetingParserController.cs - PRODUCTION READY WITH TRANSLITERATION

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
            Console.WriteLine($"📝 INPUT TEXT: {userText}");

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

            // 🔹 NAME → Clean, Transliterate, and Capitalize
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                Console.WriteLine($"🔄 Processing name: {aiResult.ClientName}");
                
                // Step 1: Clean the name (remove titles, numbers, etc.)
                aiResult.ClientName = HindiRomanTransliterator.CleanName(aiResult.ClientName);
                Console.WriteLine($"  ✓ After cleaning: {aiResult.ClientName}");
                
                // Step 2: Transliterate if Hindi is present
                aiResult.ClientName = HindiRomanTransliterator.ToRoman(aiResult.ClientName);
                Console.WriteLine($"  ✓ After transliteration: {aiResult.ClientName}");
                
                Console.WriteLine($"✅ FINAL NAME: {aiResult.ClientName}");
            }

            // 🔹 DATE
            if (!string.IsNullOrWhiteSpace(aiResult.MeetingDate))
            {
                aiResult.MeetingDate = DateHelper.ResolveDate(aiResult.MeetingDate);
                Console.WriteLine($"✅ FINAL DATE: {aiResult.MeetingDate}");
            }

            // 🔹 TIME
            if (!string.IsNullOrWhiteSpace(aiResult.StartTime))
            {
                aiResult.StartTime = TimeHelper.Normalize(aiResult.StartTime, userText);
                Console.WriteLine($"✅ FINAL START TIME: {aiResult.StartTime}");
            }

            if (!string.IsNullOrWhiteSpace(aiResult.EndTime))
            {
                aiResult.EndTime = TimeHelper.Normalize(aiResult.EndTime, userText);
                Console.WriteLine($"✅ FINAL END TIME: {aiResult.EndTime}");
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
        // 🔥 IMPROVED GROQ CALL - UPDATED PROMPT
        // ==================================================
        private async Task<AiResult> CallGroqAsync(string text, string apiKey)
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = "llama-3.3-70b-versatile",
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are an expert at extracting meeting information from Hindi, English, or Hinglish speech.

**EXTRACTION RULES:**

1. **clientName**: Extract EXACTLY as spoken (preserve original script)
   - Hindi: ""भूमिका टेकम"" → ""भूमिका टेकम""
   - English: ""Rakesh Sharma"" → ""Rakesh Sharma""
   - Hinglish: ""Nandini Jain"" → ""Nandini Jain""
   - Remove titles: Mr, Mrs, Shri, etc.

2. **mobileNumber**: 10-digit Indian phone number (digits only)
   - Extract: ""9876543210""
   - Ignore date/time numbers

3. **meetingDate**: Date phrase EXACTLY as spoken
   - ""tomorrow"" → ""tomorrow""
   - ""kal"" → ""kal""
   - ""22 december"" → ""22 december""
   - ""22 दिसंबर"" → ""22 दिसंबर""

4. **startTime**: Start time EXACTLY as mentioned
   - ""5 PM"" → ""5 PM""
   - ""4:30"" → ""4:30""
   - ""5.30"" → ""5.30"" (keep dot format)

5. **endTime**: End time EXACTLY as mentioned
   - ""8 PM"" → ""8 PM""
   - ""5:30"" → ""5:30""
   - ""5.30"" → ""5.30"" (keep dot format)

**TIME FORMAT RULES:**

For ""X to Y"" or ""X se Y"":
✅ ""5 pm to 5.30 pm"" → startTime=""5 pm"", endTime=""5.30 pm""
✅ ""4 se 4.30"" → startTime=""4"", endTime=""4.30""

⚠️ NEVER extract minutes alone:
❌ ""5 pm to 5.30 pm"" → endTime=""30 pm"" (WRONG!)
✅ ""5 pm to 5.30 pm"" → endTime=""5.30 pm"" (CORRECT!)

**DEFAULT VALUES:**
- Use empty string """" if field not found
- Do NOT guess or make up information

**EXAMPLES:**

Input: ""भूमिका टेकम के साथ मीटिंग कल 5 बजे से 6 बजे""
Output: {""clientName"": ""भूमिका टेकम"", ""mobileNumber"": """", ""meetingDate"": ""कल"", ""startTime"": ""5"", ""endTime"": ""6""}

Input: ""Rakesh Sharma meeting tomorrow 4 PM to 5 PM mobile 9876543210""
Output: {""clientName"": ""Rakesh Sharma"", ""mobileNumber"": ""9876543210"", ""meetingDate"": ""tomorrow"", ""startTime"": ""4 PM"", ""endTime"": ""5 PM""}

Input: ""Nandini Jain 8 फरवरी 5 pm to 5:30 pm""
Output: {""clientName"": ""Nandini Jain"", ""mobileNumber"": """", ""meetingDate"": ""8 फरवरी"", ""startTime"": ""5 pm"", ""endTime"": ""5:30 pm""}

Return ONLY valid JSON, no markdown, no extra text."
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

        private AiResult ParseAiJson(string json)
        {
            try
            {
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
                
                Console.WriteLine($"✅ PARSED JSON: Name='{result?.ClientName}', Mobile={result?.MobileNumber}, Date={result?.MeetingDate}");
                
                return result ?? new AiResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ JSON PARSE ERROR: " + ex.Message);
                Console.WriteLine("❌ PROBLEMATIC JSON: " + json);
                return new AiResult();
            }
        }

        private void ApplyRegexFallback(string text, AiResult result)
        {
            Console.WriteLine("🔍 APPLYING REGEX FALLBACK...");

            // Mobile Number
            if (string.IsNullOrWhiteSpace(result.MobileNumber))
            {
                var textNoSpaces = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");
                var mobileMatch = Regex.Match(textNoSpaces, @"\b[6-9]\d{9}\b");
                
                if (mobileMatch.Success)
                {
                    result.MobileNumber = mobileMatch.Value;
                    Console.WriteLine($"📱 REGEX FOUND MOBILE: {result.MobileNumber}");
                }
            }

            // Client Name (keep in original script, will be transliterated later)
            if (string.IsNullOrWhiteSpace(result.ClientName))
            {
                var hindiMatch = Regex.Match(text, @"([\p{L}\s]+?)\s+(?:ke\s+saath|साथ|के\s+साथ)\s+(?:meeting|मीटिंग)", RegexOptions.IgnoreCase);
                var englishMatch = Regex.Match(text, @"(?:meeting|meet)\s+(?:with\s+)?([\p{L}\s]+?)(?:\s+(?:on|at|tomorrow|today|kal))", RegexOptions.IgnoreCase);
                
                if (hindiMatch.Success)
                {
                    result.ClientName = hindiMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (Hindi pattern): {result.ClientName}");
                }
                else if (englishMatch.Success)
                {
                    result.ClientName = englishMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (English pattern): {result.ClientName}");
                }
            }

            // Date - Enhanced
            if (string.IsNullOrWhiteSpace(result.MeetingDate))
            {
                if (Regex.IsMatch(text, @"\b(today|aaj|आज)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "today";
                }
                else if (Regex.IsMatch(text, @"\b(tomorrow|kal|कल)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "tomorrow";
                }
                else if (Regex.IsMatch(text, @"\b(parso|परसों|day\s+after\s+tomorrow)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "parso";
                }
            }

            // Time Range
            if (string.IsNullOrWhiteSpace(result.StartTime) || string.IsNullOrWhiteSpace(result.EndTime))
            {
                var timeMatch = Regex.Match(text, @"(\d{1,2})(?::(\d{2}))?\s*(?:pm|am|पीएम|एएम)?\s*(?:se|to|से)\s*(\d{1,2})(?::(\d{2}))?\s*(?:pm|am|पीएम|एएम)?", RegexOptions.IgnoreCase);
                
                if (timeMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        var startHour = timeMatch.Groups[1].Value;
                        var startMin = timeMatch.Groups[2].Success ? timeMatch.Groups[2].Value : "";
                        result.StartTime = string.IsNullOrEmpty(startMin) ? startHour : $"{startHour}:{startMin}";
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        var endHour = timeMatch.Groups[3].Value;
                        var endMin = timeMatch.Groups[4].Success ? timeMatch.Groups[4].Value : "";
                        result.EndTime = string.IsNullOrEmpty(endMin) ? endHour : $"{endHour}:{endMin}";
                        Console.WriteLine($"🕐 REGEX FOUND END TIME: {result.EndTime}");
                    }
                }
            }
        }

        private List<string> Validate(AiResult r)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(r.ClientName))
                errors.Add("Client name missing");

            if (string.IsNullOrWhiteSpace(r.MobileNumber))
                errors.Add("Mobile number missing");
            else if (!Regex.IsMatch(r.MobileNumber, @"^[6-9]\d{9}$"))
                errors.Add("Invalid mobile number format");

            if (string.IsNullOrWhiteSpace(r.MeetingDate))
                errors.Add("Meeting date missing");

            if (string.IsNullOrWhiteSpace(r.StartTime))
                errors.Add("Start time missing");

            if (string.IsNullOrWhiteSpace(r.EndTime))
                errors.Add("End time missing");

            return errors;
        }
    }

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