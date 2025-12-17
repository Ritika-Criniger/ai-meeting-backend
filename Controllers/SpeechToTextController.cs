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
                return BadRequest("Audio file missing");

            var groqKey = _config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(groqKey))
                return StatusCode(500, "Groq API key missing");

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", groqKey);

                using var form = new MultipartFormDataContent();

                using var ms = new MemoryStream();
                await audio.CopyToAsync(ms);
                ms.Position = 0;

                var audioContent = new StreamContent(ms);
                audioContent.Headers.ContentType =
                    new MediaTypeHeaderValue(audio.ContentType);

                form.Add(audioContent, "file", audio.FileName);

                // 🔥 GROQ WHISPER MODEL
                form.Add(new StringContent("whisper-large-v3"), "model");

                form.Add(new StringContent("en"), "language");


                var response = await http.PostAsync(
                    "https://api.groq.com/openai/v1/audio/transcriptions",
                    form
                );

                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, raw);

                using var doc = JsonDocument.Parse(raw);
                var text = doc.RootElement.GetProperty("text").GetString() ?? "";

                // ==================================================
                // 🔥 BASIC CLEANUP ONLY (NO SEMANTIC CHANGES)
                // ==================================================
                text = Regex.Replace(text, @"\s+", " ").Trim();

                return Ok(new
                {
                    success = true,
                    provider = "groq",
                    text
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
