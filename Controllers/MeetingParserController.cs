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

        // Simple helper: capitalize each word for pure English names
        private static string CapitalizeWordsSimple(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var word = parts[i].ToLowerInvariant();
                if (word.Length == 0) continue;
                parts[i] = char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word.Substring(1) : string.Empty);
            }

            return string.Join(" ", parts);
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
            // 3️⃣ NAME NORMALIZATION (Hindi vs pure English)
            // ===============================
            if (!string.IsNullOrWhiteSpace(aiResult.ClientName))
            {
                Console.WriteLine($"🔄 STARTING NAME PROCESSING: '{aiResult.ClientName}'");

                // Step 1: Clean (remove titles, numbers, etc.)
                aiResult.ClientName = HindiRomanTransliterator.CleanName(aiResult.ClientName);
                Console.WriteLine($"  Step 1 - Cleaned: '{aiResult.ClientName}'");

                // If the overall sentence has any Hindi characters, treat it as Hindi flow,
                // otherwise trust the model's English name and just clean + capitalize it.
                bool sentenceHasHindi = HindiRomanTransliterator.ContainsHindi(userText);

                if (sentenceHasHindi)
                {
                    // 🔤 HINDI / MIXED: transliterate + Indian-specific corrections
                    aiResult.ClientName = HindiRomanTransliterator.ToRoman(aiResult.ClientName);
                    Console.WriteLine($"  Step 2 - Transliterated (Hindi) → '{aiResult.ClientName}'");

                    aiResult.ClientName = IndianNameCorrector.CorrectName(aiResult.ClientName);
                    Console.WriteLine($"  Step 3 - Corrected (Hindi) → '{aiResult.ClientName}'");
                }
                else
                {
                    // 🔤 PURE ENGLISH SENTENCE:
                    // Do NOT try to re-guess the name from the sentence.
                    // Just keep the model's clientName as-is with simple capitalization.
                    aiResult.ClientName = CapitalizeWordsSimple(aiResult.ClientName);
                    Console.WriteLine($"  Step 2 - English name kept with simple caps → '{aiResult.ClientName}'");
                }

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

3. **English / Hinglish Names:** Copy EXACTLY as heard, do NOT change to a different valid name
   - ""John Doe"", ""Ammulya Chowdhury"", ""Akshat Jain"", ""Rani Verma"", ""Nidhi Mandliya"", ""Nitesh Patidar"", ""Akash Daima""
   - ❌ NEVER change one valid Indian name into another (for example: do NOT change ""Akshat"" to ""Asha"" or ""Rani"" to ""Anil"" or ""Nitesh Patidar"" to any other name)

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

8. **OUTPUT SCHEMA (IMPORTANT):**
   You MUST return a single JSON object with these fields:
   {
     ""clientName"": string,
     ""mobileNumber"": string,
     ""meetingDate"": string,
     ""startTime"": string,
     ""endTime"": string,
     ""clientNameSpanStart"": integer (0-based index in the original user text, or -1 if unknown),
     ""clientNameSpanEnd"": integer (exclusive index, or -1 if unknown),
     ""clientNameSpanText"": string (exact substring for the name, or empty)
   }

**EXAMPLES (DO NOT CHANGE NAMES):**

Input: ""नीरज कुमावत कल शामको चार बजे""
Output: {""clientName"": ""Neeraj Kumawat"", ""mobileNumber"": """", ""meetingDate"": ""kal"", ""startTime"": ""4"", ""endTime"": """"}

Input: ""विक्रान्त धारा meeting tomorrow 5 pm to 6 pm""
Output: {""clientName"": ""Vikrant Dhara"", ""mobileNumber"": """", ""meetingDate"": ""tomorrow"", ""startTime"": ""5 pm"", ""endTime"": ""6 pm""}

Input: ""Ammulya Chowdhury 8 feb 5:30 pm call 9876543210""
Output: {""clientName"": ""Ammulya Chowdhury"", ""mobileNumber"": ""9876543210"", ""meetingDate"": ""8 feb"", ""startTime"": ""5:30 pm"", ""endTime"": """"}

Input: ""Connect with Akshat Jain tomorrow evening from 4 pm to 5 pm via mobile number 9123456789""
Output: {""clientName"": ""Akshat Jain"", ""mobileNumber"": ""9123456789"", ""meetingDate"": ""tomorrow"", ""startTime"": ""4 pm"", ""endTime"": ""5 pm""}

Input: ""Schedule meeting with Rani Verma tomorrow from 5 pm to 6 pm, mobile number 6267304521""
Output: {""clientName"": ""Rani Verma"", ""mobileNumber"": ""6267304521"", ""meetingDate"": ""tomorrow"", ""startTime"": ""5 pm"", ""endTime"": ""6 pm""}

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
                // Collapse spaces between digits: "98765 43210" → "9876543210"
                var digitsCollapsed = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");

                // Allow optional +91 / 91 / 0 prefixes and hyphens/spaces:
                // "+91 98765-43210", "0919876543210", "919876543210"
                var mobileMatch = Regex.Match(
                    digitsCollapsed,
                    @"(?:\+?91|0)?([6-9]\d{9})",
                    RegexOptions.IgnoreCase
                );

                if (mobileMatch.Success)
                {
                    // Group 1 always contains the clean 10‑digit Indian mobile
                    result.MobileNumber = mobileMatch.Groups[1].Value;
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
                // Support Hindi + Hinglish connectors: "7 se 8", "7 sa 8", "7 to 8", "7 till 8", "7 se 8 tak"
                var timeMatch = Regex.Match(text, 
                    @"(\d{1,2})(?:[:\.](\d{2}))?\s*(?:pm|am|पीएम|एएम)?\s*(?:se|sa|say|to|till|tak|से|तक)\s*(\d{1,2})(?:[:\.](\d{2}))?\s*(?:pm|am|पीएम|एएम)?", 
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

            // 🔁 Mobile number is OPTIONAL for the meeting,
            // but if it is present then it must be a valid Indian 10‑digit number.
            if (!string.IsNullOrWhiteSpace(r.MobileNumber) &&
                !Regex.IsMatch(r.MobileNumber, @"^[6-9]\d{9}$"))
            {
                errors.Add("Invalid mobile number");
            }

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

        // Optional span information for the client name inside the original text
        public int ClientNameSpanStart { get; set; } = -1;
        public int ClientNameSpanEnd { get; set; } = -1;
        public string ClientNameSpanText { get; set; } = "";
    }
}