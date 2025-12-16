using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AiMeetingBackend.Controllers
{
    [ApiController]
    [Route("api/speech-to-text")]
    public class SpeechToTextController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SpeechToTextController> _logger;

        public SpeechToTextController(
            IConfiguration config,
            ILogger<SpeechToTextController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ConvertSpeechToText([FromForm] IFormFile audio)
        {
            if (audio == null || audio.Length == 0)
            {
                _logger.LogWarning("❌ No audio file received");
                return BadRequest(new { success = false, error = "Audio file missing" });
            }

            try
            {
                var apiKey = _config["Groq:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("❌ Groq API key not configured");
                    return StatusCode(500, new { success = false, error = "API key not configured" });
                }

                _logger.LogInformation($"🎧 Audio received: {audio.FileName}, {audio.Length} bytes, Type: {audio.ContentType}");

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var form = new MultipartFormDataContent();

                // 🔥 Copy to memory stream to avoid stream closure issues
                using var memoryStream = new MemoryStream();
                await audio.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var audioContent = new StreamContent(memoryStream);
                
                // 🔥 Set correct content type based on file extension
                var contentType = audio.ContentType;
                if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
                {
                    var extension = Path.GetExtension(audio.FileName).ToLower();
                    contentType = extension switch
                    {
                        ".m4a" => "audio/m4a",
                        ".mp3" => "audio/mpeg",
                        ".wav" => "audio/wav",
                        ".webm" => "audio/webm",
                        ".ogg" => "audio/ogg",
                        _ => "audio/mpeg"
                    };
                }
                
                audioContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(audioContent, "file", audio.FileName);

                // Whisper API parameters
                form.Add(new StringContent("whisper-large-v3"), "model");
                form.Add(new StringContent("hi"), "language"); // Hindi priority
                form.Add(new StringContent("0.0"), "temperature"); // More deterministic

                _logger.LogInformation("📤 Sending to Groq Whisper API...");

                var response = await httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/audio/transcriptions",
                    form
                );

                var rawResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Whisper API error ({response.StatusCode}): {rawResponse}");
                    return StatusCode((int)response.StatusCode, 
                        new { success = false, error = "Whisper transcription failed", details = rawResponse });
                }

                _logger.LogInformation($"✅ Whisper API response received");

                using var doc = JsonDocument.Parse(rawResponse);

                if (!doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    _logger.LogWarning("⚠️ No 'text' property in response");
                    return Ok(new { success = false, text = "", error = "No text in response" });
                }

                var transcribedText = textElement.GetString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    _logger.LogWarning("⚠️ Empty transcription");
                    return Ok(new { success = false, text = "", error = "Empty transcription" });
                }

                _logger.LogInformation($"📝 Transcription: {transcribedText}");

                return Ok(new
                {
                    success = true,
                    text = transcribedText
                });
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("❌ Request timeout");
                return StatusCode(504, new { success = false, error = "Request timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Speech-to-text processing failed");
                return StatusCode(500, new { success = false, error = "Processing failed", details = ex.Message });
            }
        }
    }
}