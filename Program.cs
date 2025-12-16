var builder = WebApplication.CreateBuilder(args);

// ❌ REMOVE UseUrls completely (Railway handles port)
// builder.WebHost.UseUrls("http://0.0.0.0:5241", "http://localhost:5241");

// Add Controllers
builder.Services.AddControllers();

// Add CORS (Required for React Native / Expo)
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

// Increase max file upload size (for audio files)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50 MB
});

var app = builder.Build();

// Use CORS FIRST
app.UseCors("AllowAll");

// Swagger (Railway prod me bhi ok)
app.UseSwagger();
app.UseSwaggerUI();

// Map Controllers
app.MapControllers();

// ❗ IMPORTANT: DO NOT PASS ANY URL OR PORT HERE
app.Run();
