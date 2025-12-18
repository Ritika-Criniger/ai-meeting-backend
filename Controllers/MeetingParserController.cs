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

            var openaiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(openaiKey))
            {
                return StatusCode(500, "OpenAI API key missing");
            }

            var userText = request.Text.Trim();
            Console.WriteLine($"📝 INPUT TEXT: {userText}");

            // ===============================
            // 1️⃣ CALL GPT-4 MINI FOR EXTRACTION
            // ===============================
            var aiResult = await CallOpenAIAsync(userText, openaiKey);

            // ===============================
            // 2️⃣ SAFE FALLBACK (REGEX ONLY IF EMPTY)
            // ===============================
            ApplyRegexFallback(userText, aiResult);

            // ===============================
            // 3️⃣ NORMALIZATION (HELPERS)
            // ===============================

            // 🔹 NAME → Clean, Transliterate ONLY if Hindi, Capitalize
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                Console.WriteLine($"🔄 Processing name: {aiResult.ClientName}");
                
                // Step 1: Clean the name (remove titles, numbers, etc.)
                aiResult.ClientName = HindiRomanTransliterator.CleanName(aiResult.ClientName);
                Console.WriteLine($"  ✓ After cleaning: {aiResult.ClientName}");
                
                // Step 2: Transliterate ONLY if contains Hindi characters
                if (HindiRomanTransliterator.ContainsHindi(aiResult.ClientName))
                {
                    aiResult.ClientName = HindiRomanTransliterator.ToRoman(aiResult.ClientName);
                    Console.WriteLine($"  ✓ After transliteration: {aiResult.ClientName}");
                }
                else
                {
                    // English name - just capitalize properly
                    aiResult.ClientName = CapitalizeEnglishName(aiResult.ClientName);
                    Console.WriteLine($"  ✓ English name capitalized: {aiResult.ClientName}");
                }
                
                Console.WriteLine($"✅ FINAL NAME: {aiResult.ClientName}");
            }

            // 🔹 DATE - with comprehensive validation
            if (!string.IsNullOrWhiteSpace(aiResult.MeetingDate))
            {
                var originalDate = aiResult.MeetingDate;
                aiResult.MeetingDate = DateHelper.ResolveDate(aiResult.MeetingDate);
                
                // Validate the resolved date
                if (!DateHelper.IsValidDate(aiResult.MeetingDate))
                {
                    Console.WriteLine($"⚠️ INVALID DATE RESOLVED: {originalDate} → {aiResult.MeetingDate}");
                    aiResult.MeetingDate = ""; // Clear invalid date
                }
                else
                {
                    Console.WriteLine($"✅ FINAL DATE: {aiResult.MeetingDate}");
                }
            }

            // 🔹 TIME - with context-aware normalization
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

            // 🔹 VALIDATE TIME RANGE
            if (!string.IsNullOrWhiteSpace(aiResult.StartTime) && 
                !string.IsNullOrWhiteSpace(aiResult.EndTime))
            {
                if (!TimeHelper.IsValidTimeRange(aiResult.StartTime, aiResult.EndTime))
                {
                    Console.WriteLine($"⚠️ INVALID TIME RANGE: {aiResult.StartTime} to {aiResult.EndTime}");
                }
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
        // 🔥 GPT-4 MINI CALL - PRODUCTION OPTIMIZED
        // ==================================================
        private async Task<AiResult> CallOpenAIAsync(string text, string apiKey)
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = "gpt-4o-mini", // Cost-effective and fast
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are an expert at extracting meeting information from Hindi, English, or Hinglish speech transcriptions.

**CRITICAL EXTRACTION RULES:**

1. **clientName**: Extract EXACTLY as spoken, preserve original script and case
   - Hindi: ""भूमिका टेकम"" → ""भूमिका टेकम""
   - English: ""Rakesh Sharma"" → ""Rakesh Sharma"" (preserve capitals)
   - English: ""rakesh sharma"" → ""rakesh sharma"" (preserve lowercase)
   - Hinglish: ""Nandini Jain"" → ""Nandini Jain""
   - NEVER change capitalization
   - Remove titles: Mr, Mrs, Shri, Ms, Dr, etc.

2. **mobileNumber**: 10-digit Indian phone number (digits only)
   - Extract: ""9876543210""
   - Ignore date/time numbers
   - Must start with 6, 7, 8, or 9

3. **meetingDate**: Date phrase EXACTLY as spoken
   - ""tomorrow"" → ""tomorrow""
   - ""kal"" → ""kal""
   - ""22 december"" → ""22 december""
   - ""22 दिसंबर"" → ""22 दिसंबर""
   - ""parso"" → ""parso""

4. **startTime**: Start time with format preserved
   - ""5 PM"" → ""5 PM""
   - ""4:30"" → ""4:30""
   - ""5.30 PM"" → ""5.30 PM"" (keep dot if present)
   - Extract complete time, not just minutes

5. **endTime**: End time with format preserved
   - ""8 PM"" → ""8 PM""
   - ""5:30"" → ""5:30""
   - ""5.30 PM"" → ""5.30 PM""

**TIME EXTRACTION - CRITICAL:**

For patterns like ""X to Y"" or ""X se Y"":
✅ CORRECT: ""5 pm to 5.30 pm"" → startTime=""5 pm"", endTime=""5.30 pm""
✅ CORRECT: ""4 se 4.30"" → startTime=""4"", endTime=""4.30""
❌ WRONG: ""5 pm to 5.30 pm"" → endTime=""30 pm"" (NEVER extract minutes alone!)
❌ WRONG: ""4 to 5"" → startTime=""to 5"" (NEVER include prepositions!)

**AM/PM DETECTION:**
- If text contains: ""shaam"", ""evening"", ""raat"", ""night"", ""ko"", ""baad"" → likely PM
- If text contains: ""subah"", ""morning"", ""savere"" → likely AM
- Include AM/PM in extracted time if mentioned

**DEFAULT VALUES:**
- Use empty string """" if field not found
- Do NOT guess or make up information
- Do NOT invent names, numbers, or times

**EXAMPLES:**

Input: ""भूमिका टेकम के साथ मीटिंग कल 5 बजे से 6 बजे""
Output: {""clientName"": ""भूमिका टेकम"", ""mobileNumber"": """", ""meetingDate"": ""कल"", ""startTime"": ""5"", ""endTime"": ""6""}

Input: ""Rakesh Sharma meeting tomorrow 4 PM to 5 PM mobile 9876543210""
Output: {""clientName"": ""Rakesh Sharma"", ""mobileNumber"": ""9876543210"", ""meetingDate"": ""tomorrow"", ""startTime"": ""4 PM"", ""endTime"": ""5 PM""}

Input: ""nandini jain 8 february 5 pm to 5:30 pm call 9123456789""
Output: {""clientName"": ""nandini jain"", ""mobileNumber"": ""9123456789"", ""meetingDate"": ""8 february"", ""startTime"": ""5 pm"", ""endTime"": ""5:30 pm""}

Input: ""Meet with john doe tomorrow at 10""
Output: {""clientName"": ""john doe"", ""mobileNumber"": """", ""meetingDate"": ""tomorrow"", ""startTime"": ""10"", ""endTime"": """"}

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
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"
                    )
                );

                var raw = await response.Content.ReadAsStringAsync();
                Console.WriteLine("🔥 RAW OPENAI RESPONSE: " + raw);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ OpenAI API Error: " + raw);
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
                Console.WriteLine("❌ OPENAI CALL ERROR: " + ex.Message);
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

            // Mobile Number - with strict validation
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

            // Client Name - preserve original case
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

            // Date
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

            // Time Range - improved extraction
            if (string.IsNullOrWhiteSpace(result.StartTime) || string.IsNullOrWhiteSpace(result.EndTime))
            {
                // Pattern: "5 pm to 5.30 pm" or "4 se 4:30"
                var timeMatch = Regex.Match(text, 
                    @"(\d{1,2})(?:[:\.](\d{2}))?\s*(?:pm|am|पीएम|एएम)?\s*(?:se|to|से)\s*(\d{1,2})(?:[:\.](\d{2}))?\s*(?:pm|am|पीएम|एएम)?", 
                    RegexOptions.IgnoreCase);
                
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

        // ==================================================
        // 🔥 CAPITALIZE ENGLISH NAMES PROPERLY
        // ==================================================
        private string CapitalizeEnglishName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    // Check if already properly capitalized
                    if (char.IsUpper(words[i][0]) && words[i].Substring(1) == words[i].Substring(1).ToLower())
                    {
                        continue; // Already properly formatted
                    }
                    
                    // Otherwise, capitalize first letter
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private List<string> Validate(AiResult r)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(r.ClientName))
                errors.Add("Client name missing");
            else if (!HindiRomanTransliterator.IsValidName(r.ClientName))
                errors.Add("Invalid client name format");

            if (string.IsNullOrWhiteSpace(r.MobileNumber))
                errors.Add("Mobile number missing");
            else if (!Regex.IsMatch(r.MobileNumber, @"^[6-9]\d{9}$"))
                errors.Add("Invalid mobile number format (must be 10 digits starting with 6-9)");

            if (string.IsNullOrWhiteSpace(r.MeetingDate))
                errors.Add("Meeting date missing");
            else if (!DateHelper.IsValidDate(r.MeetingDate))
                errors.Add("Invalid or past meeting date");

            if (string.IsNullOrWhiteSpace(r.StartTime))
                errors.Add("Start time missing");

            if (string.IsNullOrWhiteSpace(r.EndTime))
                errors.Add("End time missing");

            if (!string.IsNullOrWhiteSpace(r.StartTime) && 
                !string.IsNullOrWhiteSpace(r.EndTime) &&
                !TimeHelper.IsValidTimeRange(r.StartTime, r.EndTime))
                errors.Add("Invalid time range (end time must be after start time)");

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
        public double Confidence { get; set; } = 0.95;
    }
}