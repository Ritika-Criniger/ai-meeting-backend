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

            var groqKey = _config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(groqKey))
                return StatusCode(500, new { success = false, message = "Groq API key missing" });

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", groqKey);

                using var form = new MultipartFormDataContent();

                using var ms = new MemoryStream();
                await audio.CopyToAsync(ms);
                ms.Position = 0;

                var audioContent = new StreamContent(ms);
                audioContent.Headers.ContentType =
                    new MediaTypeHeaderValue(audio.ContentType ?? "audio/m4a");

                form.Add(audioContent, "file", audio.FileName ?? "audio.m4a");

                // 🔥 GROQ WHISPER MODEL
                form.Add(new StringContent("whisper-large-v3"), "model");

                // 🔥 CRITICAL FIX: Force English transcription
                // This makes Whisper transcribe Hindi audio into English (romanized)
                form.Add(new StringContent("en"), "language");
                Console.WriteLine("🌍 FORCING ENGLISH TRANSCRIPTION");

                // Add response format for better output
                form.Add(new StringContent("json"), "response_format");

                // Temperature 0 for consistent results
                form.Add(new StringContent("0"), "temperature");

                var response = await http.PostAsync(
                    "https://api.groq.com/openai/v1/audio/transcriptions",
                    form
                );

                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ Groq API Error: " + raw);
                    return StatusCode(500, new { success = false, message = "Transcription failed", error = raw });
                }

                using var doc = JsonDocument.Parse(raw);
                var text = doc.RootElement.GetProperty("text").GetString() ?? "";

                Console.WriteLine($"🎤 RAW TRANSCRIBED TEXT (English): {text}");

                // ==================================================
                // 🔥 SMART CLEANUP (preserve meaning, remove noise)
                // ==================================================
                text = CleanupTranscription(text);

                Console.WriteLine($"✅ CLEANED TEXT: {text}");

                return Ok(new
                {
                    success = true,
                    provider = "groq",
                    model = "whisper-large-v3",
                    language = "en",
                    text,
                    length = text.Length
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SPEECH-TO-TEXT ERROR: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==================================================
        // 🔥 SMART CLEANUP FUNCTION
        // ==================================================
        private string CleanupTranscription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // 1. Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // 2. Remove common filler words (English + Hindi romanized)
            var fillers = new[]
            {
                "um", "uh", "hmm", "aa", "eh", "err", "ahh",
                "toh", "wo", "yaar", "bhai"
            };

            foreach (var filler in fillers)
            {
                text = Regex.Replace(text, $@"\b{filler}\b", "", RegexOptions.IgnoreCase);
            }

            // 3. Fix common transcription errors
            text = text.Replace("  ", " "); // Double spaces
            text = Regex.Replace(text, @"\s+", " "); // Multiple spaces

            // 4. Remove leading/trailing punctuation noise
            text = Regex.Replace(text, @"^[,.\-\s]+", "");
            text = Regex.Replace(text, @"[,.\-\s]+$", "");

            // 5. Fix spacing around numbers (phone numbers especially)
            // "98765 43210" → "9876543210"
            text = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");

            // 6. Normalize case - capitalize first letter of each word
            text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());

            return text.Trim();
        }
    }
}