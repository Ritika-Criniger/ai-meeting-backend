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

            // 🔹 NAME → Clean and normalize
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                // First clean the name
                aiResult.ClientName = HindiRomanTransliterator.CleanName(aiResult.ClientName);
                
                // Then transliterate if Hindi is present
                aiResult.ClientName = HindiRomanTransliterator.ToRoman(aiResult.ClientName);
                
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
        // 🔥 IMPROVED GROQ CALL WITH ENHANCED PROMPT
        // ==================================================
        private async Task<AiResult> CallGroqAsync(string text, string apiKey)
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

           // 🔥 FIXED GROQ PROMPT - Place this in your CallGroqAsync method

// 🔥 SIMPLIFIED GROQ PROMPT - No transliteration needed now!
// Replace in CallGroqAsync() method in MeetingParserController.cs

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
            content = @"You are an expert meeting information extractor. All input is already in English (romanized). Extract ONLY these fields and return VALID JSON.

**FIELDS TO EXTRACT:**

1. **clientName**: Full person's name EXACTLY as given
   - ""Bhoomika Tekam"" → ""Bhoomika Tekam""
   - ""Nandini Jain"" → ""Nandini Jain""
   - ""Gauri"" → ""Gauri""
   - ""Rakesh Sharma"" → ""Rakesh Sharma""
   - Remove titles: Mr, Mrs, Shri, etc.

2. **mobileNumber**: 10-digit phone number (digits only)
   - Extract: ""9876543210""
   - Ignore date/time numbers

3. **meetingDate**: Date phrase EXACTLY as spoken
   - ""tomorrow"" → ""tomorrow""
   - ""kal"" → ""kal""
   - ""22 december"" → ""22 december""
   - ""8 february"" → ""8 february""
   - ""22 disember"" → ""22 december"" (fix common misspellings)
   - ""8 farvari"" → ""8 february""

4. **startTime**: Start time EXACTLY as mentioned
   - ""5 PM"" → ""5 PM""
   - ""10"" → ""10""
   - ""4:30"" → ""4:30""
   - ""4.30"" → ""4:30"" (convert dot to colon)

5. **endTime**: End time EXACTLY as mentioned
   - ""8 PM"" → ""8 PM""
   - ""5:30"" → ""5:30""
   - ""5.30"" → ""5:30"" (convert dot to colon)

**🔥 CRITICAL TIME RULES:**

For ""X to Y"" or ""X se Y"":
- Extract X → startTime
- Extract Y → endTime
- Do NOT add, calculate, or modify

✅ CORRECT Examples:
""5 pm to 5.30 pm"" → start=""5 PM"", end=""5:30 PM""
""4 se 4:30"" → start=""4"", end=""4:30""
""10 to 11"" → start=""10"", end=""11""

**OTHER RULES:**

⚠️ NAME EXTRACTION:
- Keep names exactly as provided
- Remove titles only
- Don't modify spelling

⚠️ DATE NORMALIZATION:
- Fix common Hindi month misspellings:
  - ""disember"" → ""december""
  - ""farvari"" / ""farwari"" → ""february""
  - ""janvari"" → ""january""
  - ""oktombar"" → ""october""
  - ""navambar"" → ""november""

⚠️ TIME NORMALIZATION:
- Convert dots to colons: ""5.30"" → ""5:30""

⚠️ DEFAULT VALUES:
- Use empty string """" if field not found

**EXAMPLES:**

Input: ""Bhoomika Tekam""
Output: {""clientName"": ""Bhoomika Tekam"", ""mobileNumber"": """", ""meetingDate"": """", ""startTime"": """", ""endTime"": """"}

Input: ""Nandini Jain""
Output: {""clientName"": ""Nandini Jain"", ""mobileNumber"": """", ""meetingDate"": """", ""startTime"": """", ""endTime"": """"}

Input: ""Gauri""
Output: {""clientName"": ""Gauri"", ""mobileNumber"": """", ""meetingDate"": """", ""startTime"": """", ""endTime"": """"}

Input: ""22 disember""
Output: {""clientName"": """", ""mobileNumber"": """", ""meetingDate"": ""22 december"", ""startTime"": """", ""endTime"": """"}

Input: ""8 farvari""
Output: {""clientName"": """", ""mobileNumber"": """", ""meetingDate"": ""8 february"", ""startTime"": """", ""endTime"": """"}

Input: ""5 pm to 5.30 pm""
Output: {""clientName"": """", ""mobileNumber"": """", ""meetingDate"": """", ""startTime"": ""5 PM"", ""endTime"": ""5:30 PM""}

Input: ""Rakesh Sharma 9876543210 kal 4 se 4:30""
Output: {""clientName"": ""Rakesh Sharma"", ""mobileNumber"": ""9876543210"", ""meetingDate"": ""kal"", ""startTime"": ""4"", ""endTime"": ""4:30""}

Input: ""Bhoomika Tekam 22 disember 5 pm to 5.30 pm""
Output: {""clientName"": ""Bhoomika Tekam"", ""mobileNumber"": """", ""meetingDate"": ""22 december"", ""startTime"": ""5 PM"", ""endTime"": ""5:30 PM""}

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

        // ==================================================
        // 🔥 IMPROVED JSON PARSER
        // ==================================================
        private AiResult ParseAiJson(string json)
        {
            try
            {
                // Remove markdown code blocks if present
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
                
                Console.WriteLine($"✅ PARSED: Name='{result?.ClientName}', Mobile={result?.MobileNumber}, Date={result?.MeetingDate}, Start={result?.StartTime}, End={result?.EndTime}");
                
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
        // 🔥 ENHANCED REGEX FALLBACK - IMPROVED ACCURACY
        // ==================================================
        private void ApplyRegexFallback(string text, AiResult result)
        {
            Console.WriteLine("🔍 APPLYING REGEX FALLBACK...");

            // ===============================
            // 📱 MOBILE NUMBER - ENHANCED
            // ===============================
            if (string.IsNullOrWhiteSpace(result.MobileNumber))
            {
                // Remove spaces first for phone number detection
                var textNoSpaces = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");
                
                // Pattern: 10-digit number starting with 6-9
                var mobileMatch = Regex.Match(textNoSpaces, @"\b[6-9]\d{9}\b");
                
                if (mobileMatch.Success)
                {
                    result.MobileNumber = mobileMatch.Value;
                    Console.WriteLine($"📱 REGEX FOUND MOBILE: {result.MobileNumber}");
                }
            }

            // ===============================
            // 👤 CLIENT NAME - ENHANCED
            // ===============================
            if (string.IsNullOrWhiteSpace(result.ClientName))
            {
                // Pattern 1: "X ke saath meeting" (Hindi)
                var hindiMatch = Regex.Match(text, @"([\p{L}\s]+?)\s+(?:ke\s+saath|साथ|के\s+साथ)\s+(?:meeting|मीटिंग|मिलना)", RegexOptions.IgnoreCase);
                
                // Pattern 2: "meeting with X" (English)
                var englishMatch = Regex.Match(text, @"(?:meeting|meet)\s+(?:with\s+)?([\p{L}\s]+?)(?:\s+(?:on|at|tomorrow|today|kal|parso|next|after|\d|se|to|है|hai))", RegexOptions.IgnoreCase);
                
                // Pattern 3: Name before phone number
                var beforePhoneMatch = Regex.Match(text, @"([\p{L}\s]+?)\s+[6-9]\d{9}", RegexOptions.IgnoreCase);
                
                if (hindiMatch.Success)
                {
                    result.ClientName = hindiMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (Hindi): {result.ClientName}");
                }
                else if (englishMatch.Success)
                {
                    result.ClientName = englishMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (English): {result.ClientName}");
                }
                else if (beforePhoneMatch.Success)
                {
                    result.ClientName = beforePhoneMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"👤 REGEX FOUND NAME (Before Phone): {result.ClientName}");
                }
            }

            // ===============================
            // 📅 MEETING DATE - ENHANCED
            // ===============================
            if (string.IsNullOrWhiteSpace(result.MeetingDate))
            {
                // Today
                if (Regex.IsMatch(text, @"\b(today|aaj|आज|aj)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "today";
                    Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                }
                // Tomorrow
                else if (Regex.IsMatch(text, @"\b(tomorrow|kal|कल)\b", RegexOptions.IgnoreCase) && 
                         !Regex.IsMatch(text, @"\b(next|agle|आगले)\s+(kal|कल)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "tomorrow";
                    Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                }
                // Day after tomorrow
                else if (Regex.IsMatch(text, @"\b(parso|parson|परसों|day\s+after\s+tomorrow|next\s+kal|agle\s+kal)\b", RegexOptions.IgnoreCase))
                {
                    result.MeetingDate = "parso";
                    Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                }
                // After X days
                else
                {
                    var afterMatch = Regex.Match(text, @"(?:after|baad|बाद)\s+(\d+|one|two|three|four|five|six|ek|do|teen|char|panch|che)\s+(?:days?|din|दिन|दिनों)", RegexOptions.IgnoreCase);
                    
                    if (afterMatch.Success)
                    {
                        var numStr = afterMatch.Groups[1].Value.ToLower();
                        var numMap = new Dictionary<string, string>
                        {
                            {"one", "1"}, {"two", "2"}, {"three", "3"}, {"four", "4"}, {"five", "5"}, {"six", "6"},
                            {"ek", "1"}, {"do", "2"}, {"teen", "3"}, {"char", "4"}, {"panch", "5"}, {"che", "6"}
                        };
                        
                        var days = numMap.ContainsKey(numStr) ? numMap[numStr] : numStr;
                        result.MeetingDate = $"after {days} days";
                        Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                    }
                    else
                    {
                        // Next weekday
                        var weekdayMatch = Regex.Match(text, @"\b(?:(next|agle|आगले)\s+)?(monday|tuesday|wednesday|thursday|friday|saturday|sunday|somwar|somvaar|mangalwar|mangalvaar|budhwar|budhvaar|guruwar|guruvaar|shukravar|shukravaar|shaniwar|shanivaar|raviwar|ravivaar|सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार)\b", RegexOptions.IgnoreCase);
                        
                        if (weekdayMatch.Success)
                        {
                            result.MeetingDate = weekdayMatch.Value;
                            Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                        }
                        else
                        {
                            // Specific date: "22 december 2025"
                            var dateMatch = Regex.Match(text, @"(\d{1,2})\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\s*(\d{4})?", RegexOptions.IgnoreCase);
                            
                            if (dateMatch.Success)
                            {
                                result.MeetingDate = dateMatch.Value;
                                Console.WriteLine($"📅 REGEX FOUND DATE: {result.MeetingDate}");
                            }
                        }
                    }
                }
            }

            // ===============================
            // 🕐 TIME RANGE - CRITICAL FIX
            // ===============================
            if (string.IsNullOrWhiteSpace(result.StartTime) || string.IsNullOrWhiteSpace(result.EndTime))
            {
                // Pattern 1: "7 PM se 8 PM" or "10 AM to 11 AM"
                var pmTimeMatch = Regex.Match(text, @"(\d{1,2})(?::(\d{2}))?\s*(pm|am|पीएम|एएम)\s*(?:se|to|से|टू)\s*(\d{1,2})(?::(\d{2}))?\s*(pm|am|पीएम|एएम)", RegexOptions.IgnoreCase);
                
                // Pattern 2: "4:30 se 5:45" (both with minutes)
                var timeWithMinutesMatch = Regex.Match(text, @"(\d{1,2}):(\d{2})\s*(?:se|to|से|टू)\s*(\d{1,2}):(\d{2})", RegexOptions.IgnoreCase);
                
                // Pattern 3: "4 se 4:30" (mixed format)
                var mixedFormatMatch = Regex.Match(text, @"(\d{1,2})\s*(?:se|to|से|टू)\s*(\d{1,2}):(\d{2})", RegexOptions.IgnoreCase);
                
                // Pattern 4: "4 se 5" (simple)
                var simpleTimeMatch = Regex.Match(text, @"(\d{1,2})\s*(?:se|to|से|टू)\s*(\d{1,2})(?!\:|\d)", RegexOptions.IgnoreCase);

                if (pmTimeMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        var startHour = pmTimeMatch.Groups[1].Value;
                        var startMin = pmTimeMatch.Groups[2].Success ? pmTimeMatch.Groups[2].Value : "00";
                        var startPeriod = pmTimeMatch.Groups[3].Value.ToUpper().Replace("पीएम", "PM").Replace("एएम", "AM");
                        
                        result.StartTime = $"{startHour}:{startMin} {startPeriod}";
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        var endHour = pmTimeMatch.Groups[4].Value;
                        var endMin = pmTimeMatch.Groups[5].Success ? pmTimeMatch.Groups[5].Value : "00";
                        var endPeriod = pmTimeMatch.Groups[6].Value.ToUpper().Replace("पीएम", "PM").Replace("एएम", "AM");
                        
                        result.EndTime = $"{endHour}:{endMin} {endPeriod}";
                        Console.WriteLine($"🕐 REGEX FOUND END TIME: {result.EndTime}");
                    }
                }
                else if (timeWithMinutesMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        result.StartTime = $"{timeWithMinutesMatch.Groups[1].Value}:{timeWithMinutesMatch.Groups[2].Value}";
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        result.EndTime = $"{timeWithMinutesMatch.Groups[3].Value}:{timeWithMinutesMatch.Groups[4].Value}";
                        Console.WriteLine($"🕐 REGEX FOUND END TIME: {result.EndTime}");
                    }
                }
                else if (mixedFormatMatch.Success)
                {
                    // ✅ CRITICAL: "4 se 4:30" means START=4, END=4:30
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        result.StartTime = mixedFormatMatch.Groups[1].Value;
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        result.EndTime = $"{mixedFormatMatch.Groups[2].Value}:{mixedFormatMatch.Groups[3].Value}";
                        Console.WriteLine($"🕐 REGEX FOUND END TIME: {result.EndTime}");
                    }
                }
                else if (simpleTimeMatch.Success)
                {
                    if (string.IsNullOrWhiteSpace(result.StartTime))
                    {
                        result.StartTime = simpleTimeMatch.Groups[1].Value;
                        Console.WriteLine($"🕐 REGEX FOUND START TIME: {result.StartTime}");
                    }
                    
                    if (string.IsNullOrWhiteSpace(result.EndTime))
                    {
                        result.EndTime = simpleTimeMatch.Groups[2].Value;
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