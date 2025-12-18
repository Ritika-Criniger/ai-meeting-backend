var builder = WebApplication.CreateBuilder(args);

// 🔥 RAILWAY COMPATIBLE PORT BINDING (MOST IMPORTANT)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Controllers
builder.Services.AddControllers();

// 🔥 NEW: HttpClient for Groq API calls (name translation)
builder.Services.AddHttpClient();

// CORS (Expo / React Native)
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

// Audio upload limit
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50 MB
});

var app = builder.Build();

// CORS first
app.UseCors("AllowAll");

// Swagger (prod me bhi OK)
app.UseSwagger();
app.UseSwaggerUI();

// Controllers
app.MapControllers();

// ✅ IMPORTANT: just Run
app.Run();