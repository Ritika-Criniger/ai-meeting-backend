using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiMeetingBackend.Controllers
{
    [ApiController]
    [Route("api/speech-to-text")]
    public class SpeechToTextController : ControllerBase
    {
        private readonly IConfiguration _config;

        public SpeechToTextController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(25_000_000)] // ~25 MB
        public async Task<IActionResult> Convert([FromForm] IFormFile audio)
        {
            if (audio == null || audio.Length == 0)
                return BadRequest(new { success = false, message = "Audio file missing" });

            var openaiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(openaiKey))
                return StatusCode(500, new { success = false, message = "OpenAI API key missing" });

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(60); // OpenAI can be slower than Groq
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openaiKey);

                using var form = new MultipartFormDataContent();

                using var ms = new MemoryStream();
                await audio.CopyToAsync(ms);
                ms.Position = 0;

                var audioContent = new StreamContent(ms);
                audioContent.Headers.ContentType =
                    new MediaTypeHeaderValue(audio.ContentType ?? "audio/m4a");

                form.Add(audioContent, "file", audio.FileName ?? "audio.m4a");

                // 🔥 OPENAI WHISPER MODEL
                form.Add(new StringContent("whisper-1"), "model");

                // 🔥 CRITICAL: No language parameter - let Whisper auto-detect
                // This gives best results for Hindi/English/Hinglish mix

                // Strong prompt to bias transcription towards accurate Indian names,
                // numbers, dates and times for CRM meeting scenarios.
                form.Add(new StringContent(
                    "You are transcribing audio for an enterprise CRM meeting scheduler. " +
                    "Transcribe VERY ACCURATELY in the same language (Hindi, English or Hinglish). " +
                    "Pay special attention to INDIAN FIRST NAMES and SURNAMES (for example: Neeraj, Nilesh, Rakesh, Vikrant, Kumawat, Tekam, Sharma, Verma, Jain, Agarwal, Bhumika, Gauri, etc.). " +
                    "Do NOT translate or replace names with other English words. " +
                    "Keep all digits exactly for mobile numbers and times. " +
                    "Preserve and correctly spell words related to dates and days (today, tomorrow, kal, parso, Monday, Somwar, etc.)."
                ), "prompt");

                // Response format for structured output
                form.Add(new StringContent("json"), "response_format");

                // Temperature = 0 for maximum accuracy (no creativity needed)
                form.Add(new StringContent("0"), "temperature");

                Console.WriteLine($"🎤 SENDING TO OPENAI WHISPER: {audio.FileName} ({audio.Length} bytes)");

                var response = await http.PostAsync(
                    "https://api.openai.com/v1/audio/transcriptions",
                    form
                );

                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ OpenAI API Error: {raw}");
                    return StatusCode(500, new 
                    { 
                        success = false, 
                        message = "Transcription failed", 
                        error = raw,
                        statusCode = (int)response.StatusCode
                    });
                }

                using var doc = JsonDocument.Parse(raw);
                var text = doc.RootElement.GetProperty("text").GetString() ?? "";

                Console.WriteLine($"🎤 RAW TRANSCRIBED TEXT: {text}");

                // ==================================================
                // 🔥 SMART CLEANUP (preserve meaning, remove noise)
                // ==================================================
                text = CleanupTranscription(text);

                Console.WriteLine($"✅ CLEANED TEXT: {text}");

                return Ok(new
                {
                    success = true,
                    provider = "openai",
                    model = "whisper-1",
                    text,
                    length = text.Length,
                    originalLength = audio.Length
                });
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("❌ REQUEST TIMEOUT");
                return StatusCode(504, new 
                { 
                    success = false, 
                    message = "Request timeout - audio too long or network issue" 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SPEECH-TO-TEXT ERROR: {ex.Message}");
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }

        // ==================================================
        // 🔥 ENHANCED CLEANUP FUNCTION
        // ==================================================
        private string CleanupTranscription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // 1. Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // 2. Remove common filler words (English + Hindi) - but preserve name context
            var fillers = new[]
            {
                @"\bum\b", @"\buh\b", @"\bhmm\b", @"\baa+h*\b", @"\beh\b", @"\berr\b"
            };

            foreach (var filler in fillers)
            {
                text = Regex.Replace(text, filler, "", RegexOptions.IgnoreCase);
            }

            // 3. Fix spacing around numbers (critical for phone numbers)
            // "98765 43210" → "9876543210"
            text = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");

            // 4. Normalize common speech-to-text errors
            text = text.Replace("  ", " ");
            
            // Fix common Hindi words that get mangled
            text = Regex.Replace(text, @"\bke\s+sath\b", "ke saath", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bse\s+le\b", "se", RegexOptions.IgnoreCase);

            // 5. Remove leading/trailing punctuation noise
            text = Regex.Replace(text, @"^[,.\-\s]+", "");
            text = Regex.Replace(text, @"[,.\-\s]+$", "");

            // 6. Fix time formats that get broken
            // "5 : 30" → "5:30"
            text = Regex.Replace(text, @"(\d+)\s*:\s*(\d+)", "$1:$2");
            
            // "5 . 30" → "5.30" (will be converted to 5:30 later)
            text = Regex.Replace(text, @"(\d+)\s*\.\s*(\d+)", "$1.$2");

            return text.Trim();
        }

        // ==================================================
        // 🔥 OPTIONAL: TEST ENDPOINT FOR DEBUGGING
        // ==================================================
        [HttpGet("test")]
        public IActionResult Test()
        {
            var openaiKey = _config["OpenAI:ApiKey"];
            var hasKey = !string.IsNullOrWhiteSpace(openaiKey);
            var keyPrefix = hasKey ? openaiKey.Substring(0, Math.Min(10, openaiKey.Length)) : "NOT_SET";

            return Ok(new
            {
                configured = hasKey,
                keyPrefix = keyPrefix + "...",
                model = _config["OpenAI:WhisperModel"] ?? "whisper-1"
            });
        }
    }
}