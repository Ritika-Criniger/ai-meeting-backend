// ============================================================
// FILE 2: Controllers/MeetingParserController.cs (REPLACE EXISTING)
// Location: D:\crm-ai-agent\AiMeetingBackend\Controllers\MeetingParserController.cs
// ============================================================

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
            // 1️⃣ CALL GPT-4 MINI (ENHANCED PROMPT)
            // ===============================
            var aiResult = await CallOpenAIAsync(userText, openaiKey);

            // ===============================
            // 2️⃣ REGEX FALLBACK
            // ===============================
            ApplyRegexFallback(userText, aiResult);

            // ===============================
            // 3️⃣ NAME NORMALIZATION (3-STEP PROCESS)
            // ===============================
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                Console.WriteLine($"🔄 STARTING NAME PROCESSING: '{aiResult.ClientName}'");
                
                // Step 1: Clean (remove titles, numbers, etc.)
                aiResult.ClientName = HindiRomanTransliterator.CleanName(aiResult.ClientName);
                Console.WriteLine($"  Step 1 - Cleaned: '{aiResult.ClientName}'");
                
                // Step 2: Transliterate ONLY if Hindi
                if (HindiRomanTransliterator.ContainsHindi(aiResult.ClientName))
                {
                    aiResult.ClientName = HindiRomanTransliterator.ToRoman(aiResult.ClientName);
                    Console.WriteLine($"  Step 2 - Transliterated: '{aiResult.ClientName}'");
                }
                else
                {
                    Console.WriteLine($"  Step 2 - Skipped (already Roman)");
                }
                
                // Step 3: Apply Indian Name Corrections (NEW!)
                aiResult.ClientName = IndianNameCorrector.CorrectName(aiResult.ClientName);
                Console.WriteLine($"  Step 3 - Corrected: '{aiResult.ClientName}'");
                
                Console.WriteLine($"✅ FINAL NAME: '{aiResult.ClientName}'");
            }

            // ===============================
            // 4️⃣ DATE NORMALIZATION
            // ===============================
            if (!string.IsNullOrWhiteSpace(aiResult.MeetingDate))
            {
                var originalDate = aiResult.MeetingDate;
                aiResult.MeetingDate = DateHelper.ResolveDate(aiResult.MeetingDate);
                
                if (!DateHelper.IsValidDate(aiResult.MeetingDate))
                {
                    Console.WriteLine($"⚠️ INVALID DATE: {originalDate} → {aiResult.MeetingDate}");
                    aiResult.MeetingDate = "";
                }
                else
                {
                    Console.WriteLine($"✅ FINAL DATE: {aiResult.MeetingDate}");
                }
            }

            // ===============================
            // 5️⃣ TIME NORMALIZATION
            // ===============================
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
            // 6️⃣ VALIDATION
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

        // ===============================
        // 🔥 ENHANCED GPT-4 PROMPT
        // ===============================
        private async Task<AiResult> CallOpenAIAsync(string text, string apiKey)
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = "gpt-4o-mini",
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are an expert at extracting meeting information from Hindi, English, or Hinglish speech.

**CRITICAL NAME EXTRACTION RULES:**

1. **Hindi Names → Phonetic English:**
   - ""नीरज"" sounds → ""Neeraj"" (NOT ""Naraj"")
   - ""कुमावत"" sounds → ""Kumawat"" (NOT ""Kamawat"")
   - ""विक्रम"" → ""Vikram"", ""विक्रान्त"" → ""Vikrant""
   - ""राकेश"" → ""Rakesh"", ""नीलेश"" → ""Nilesh""
   - ""भूमिका"" → ""Bhumika"", ""गौरी"" → ""Gauri""

2. **Common Patterns:**
   - 'न' at start → 'N' (Neeraj, Nilesh)
   - 'क' → 'K' (Kumawat, Kumar)
   - 'व' → 'V' or 'W' (Verma, Vikram)
   - Double vowels: 'ई' → 'ee', 'ऊ' → 'oo'

3. **English Names:** Keep as-is with proper caps
   - ""John Doe"", ""Ammulya Chowdhury""

4. **Mobile Number:**
   - 10 digits, starts with 6/7/8/9
   - ""98765 43210"" → ""9876543210""

5. **Date:** Natural language preserved
   - ""kal"" → ""kal"", ""tomorrow"" → ""tomorrow""
   - ""22 december"" → ""22 december""

6. **Time Extraction (CRITICAL):**
   ✅ ""5 pm to 5.30 pm"" → startTime=""5 pm"", endTime=""5.30 pm""
   ✅ ""4 se 4:30"" → startTime=""4"", endTime=""4:30""
   ❌ NEVER extract only minutes

7. **AM/PM Context:**
   - ""shaam"", ""evening"", ""ko"", ""baad"" → PM
   - ""subah"", ""morning"" → AM

**EXAMPLES:**

Input: ""नीरज कुमावत कल शामको चार बजे""
Output: {""clientName"": ""Neeraj Kumawat"", ""mobileNumber"": """", ""meetingDate"": ""kal"", ""startTime"": ""4"", ""endTime"": """"}

Input: ""विक्रान्त धारा meeting tomorrow 5 pm to 6 pm""
Output: {""clientName"": ""Vikrant Dhara"", ""mobileNumber"": """", ""meetingDate"": ""tomorrow"", ""startTime"": ""5 pm"", ""endTime"": ""6 pm""}

Input: ""Ammulya Chowdhury 8 feb 5:30 pm call 9876543210""
Output: {""clientName"": ""Ammulya Chowdhury"", ""mobileNumber"": ""9876543210"", ""meetingDate"": ""8 feb"", ""startTime"": ""5:30 pm"", ""endTime"": """"}

Return ONLY valid JSON."
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
                
                Console.WriteLine($"✅ PARSED: Name='{result?.ClientName}', Mobile={result?.MobileNumber}");
                
                return result ?? new AiResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ JSON PARSE ERROR: " + ex.Message);
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

            // Date
            if (string.IsNullOrWhiteSpace(result.MeetingDate))
            {
                if (Regex.IsMatch(text, @"\b(today|aaj|आज)\b", RegexOptions.IgnoreCase))
                    result.MeetingDate = "today";
                else if (Regex.IsMatch(text, @"\b(tomorrow|kal|कल)\b", RegexOptions.IgnoreCase))
                    result.MeetingDate = "tomorrow";
                else if (Regex.IsMatch(text, @"\b(parso|परसों)\b", RegexOptions.IgnoreCase))
                    result.MeetingDate = "parso";
            }

            // Time Range
            if (string.IsNullOrWhiteSpace(result.StartTime) || string.IsNullOrWhiteSpace(result.EndTime))
            {
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
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        var endHour = timeMatch.Groups[3].Value;
                        var endMin = timeMatch.Groups[4].Success ? timeMatch.Groups[4].Value : "";
                        result.EndTime = string.IsNullOrEmpty(endMin) ? endHour : $"{endHour}:{endMin}";
                    }
                }
            }
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
                errors.Add("Invalid mobile number");

            if (string.IsNullOrWhiteSpace(r.MeetingDate))
                errors.Add("Meeting date missing");
            else if (!DateHelper.IsValidDate(r.MeetingDate))
                errors.Add("Invalid meeting date");

            if (string.IsNullOrWhiteSpace(r.StartTime))
                errors.Add("Start time missing");

            if (string.IsNullOrWhiteSpace(r.EndTime))
                errors.Add("End time missing");

            if (!string.IsNullOrWhiteSpace(r.StartTime) && 
                !string.IsNullOrWhiteSpace(r.EndTime) &&
                !TimeHelper.IsValidTimeRange(r.StartTime, r.EndTime))
                errors.Add("Invalid time range");

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