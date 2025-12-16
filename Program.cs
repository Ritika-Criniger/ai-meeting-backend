var builder = WebApplication.CreateBuilder(args);

// 🔥 CRITICAL: Listen on ALL network interfaces
builder.WebHost.UseUrls("http://0.0.0.0:5241", "http://localhost:5241");

// Add Controllers
builder.Services.AddControllers();

// Add CORS (Required for React Native/Expo)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ Increase max file upload size (for audio files)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50 MB
});

var app = builder.Build();

// Use CORS FIRST (before other middleware)
app.UseCors("AllowAll");

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map Controllers
app.MapControllers();

// Startup message
var localIp = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
    .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    ?.ToString() ?? "Unknown";

Console.WriteLine("╔═══════════════════════════════════════════════╗");
Console.WriteLine("║   🚀 AI MEETING BACKEND STARTED               ║");
Console.WriteLine("╚═══════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📍 Accessible on:");
Console.WriteLine($"   • Local:   http://localhost:5241");
Console.WriteLine($"   • Network: http://{localIp}:5241");
Console.WriteLine();
Console.WriteLine("🧪 Test endpoints:");
Console.WriteLine($"   • Swagger: http://{localIp}:5241/swagger");
Console.WriteLine($"   • STT API: http://{localIp}:5241/api/speech-to-text");
Console.WriteLine($"   • Parse API: http://{localIp}:5241/api/parse-meeting");
Console.WriteLine();
Console.WriteLine($"📱 Update your React Native app to use: http://{localIp}:5241");
Console.WriteLine();

app.Run();