using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Services;
using Kitchenhelper.Infrastructure.BackgroundServices;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.RateLimiting;

// .env dosyas\u0131ndan ortam de\u011fi\u015fkenlerini y\u00fckle
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;

        var eqIndex = trimmed.IndexOf('=');
        if (eqIndex <= 0) continue;

        var key = trimmed[..eqIndex].Trim();
        var value = trimmed[(eqIndex + 1)..].Trim();
        // Sadece hen\u00fcz set edilmemi\u015f de\u011fi\u015fkenleri y\u00fckle (sistem env var'lar\u0131 \u00f6ncelikli)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

// Seeding kontrol\u00fc - e\u011fer "seed" arg\u00fcman\u0131 varsa seeding yap ve \u00e7\u0131k
if (args.Length > 0 && args[0].ToLower() == "seed")
{
    var seedBuilder = WebApplication.CreateBuilder(args);
    seedBuilder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite(seedBuilder.Configuration.GetConnectionString("Default")));

    var seedApp = seedBuilder.Build();
    using var scope = seedApp.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "recipes.json");
    var exitCode = await Kitchenhelper.Web.SeedRecipes.SeedAsync(db, jsonPath);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

// .env ortam de\u011fi\u015fkenlerini configuration'a override olarak ekle
var envOverrides = new Dictionary<string, string?>();

void MapEnv(string envKey, string configKey)
{
    var val = Environment.GetEnvironmentVariable(envKey);
    if (!string.IsNullOrEmpty(val))
        envOverrides[configKey] = val;
}

MapEnv("CONNECTION_STRING", "ConnectionStrings:Default");
MapEnv("JWT_KEY", "Jwt:Key");
MapEnv("JWT_ISSUER", "Jwt:Issuer");
MapEnv("JWT_AUDIENCE", "Jwt:Audience");
MapEnv("OPENROUTER_API_KEY", "OpenRouter:ApiKey");
MapEnv("OPENROUTER_BASE_URL", "OpenRouter:BaseUrl");
MapEnv("OPENROUTER_MODEL", "OpenRouter:Model");
MapEnv("FFMPEG_PATH", "FFmpegPath");
MapEnv("FFPROBE_PATH", "FFprobePath");
MapEnv("YTDLP_PATH", "YtDlpPath");
MapEnv("UPLOADS_PATH", "UploadsPath");
MapEnv("WHISPER_MODEL", "Whisper:Model");
MapEnv("WHISPER_PYTHON_PATH", "Whisper:PythonPath");
MapEnv("WHISPER_SCRIPT_PATH", "Whisper:ScriptPath");

// ALLOWED_ORIGINS virg\u00fclle ayr\u0131lm\u0131\u015f \u2192 dizi olarak map'le
var originsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
if (!string.IsNullOrEmpty(originsEnv))
{
    var origins = originsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (int i = 0; i < origins.Length; i++)
        envOverrides[$"AllowedOrigins:{i}"] = origins[i];
}

if (envOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(envOverrides);

// MVC
builder.Services.AddControllersWithViews();

// CORS - React ve Flutter uygulamas\u0131 i\u00e7in (izin verilen origin'ler config'den okunur)
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5039" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
builder.Services.AddScoped<IPantryService, PantryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRecipeImportService, RecipeImportService>();
builder.Services.AddScoped<IRecipeImportProcessingService, RecipeImportProcessingService>();
builder.Services.AddScoped<IAsrService, WhisperAsrService>();
builder.Services.AddScoped<IVideoAudioExtractor, FFmpegAudioExtractor>();
builder.Services.AddScoped<IUrlVideoService, UrlVideoService>();

// Video download settings and anti-ban services
builder.Services.Configure<VideoDownloadSettings>(builder.Configuration.GetSection("VideoDownload"));
builder.Services.AddSingleton<IVideoDownloadSettings>(sp =>
{
    var settings = new VideoDownloadSettings();
    builder.Configuration.GetSection("VideoDownload").Bind(settings);
    return settings;
});
builder.Services.AddSingleton<IUserAgentRotator, UserAgentRotator>();
builder.Services.AddSingleton<IRequestThrottler, RequestThrottler>();

// Draft extraction and publishing services
builder.Services.AddScoped<IRecipeDraftExtractor, GeminiDraftExtractor>();
builder.Services.AddScoped<IRecipeDraftService, RecipeDraftService>();
builder.Services.AddScoped<IRecipePublishService, RecipePublishService>();
builder.Services.AddScoped<INutritionService, GeminiNutritionService>();
builder.Services.AddScoped<IAiChatService, GeminiChatService>();

// Register background worker
builder.Services.AddHostedService<RecipeImportBackgroundWorker>();

// Register HttpClientFactory for ASR service
builder.Services.AddHttpClient();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "")),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        // Cookie'den token okuma
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader["Bearer ".Length..].Trim();
                }
                else
                {
                    context.Token = context.Request.Cookies["auth_token"];
                }

                return Task.CompletedTask;
            }
        };
    });

// Rate Limiting - Brute force korumas\u0131
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Auth endpoint'leri i\u00e7in: IP ba\u015f\u0131na dakikada 10 istek
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Genel API i\u00e7in: IP ba\u015f\u0131na dakikada 60 istek
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2
            }));
});

// Swagger/OpenAPI yap\u0131land\u0131rmas\u0131
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KitchenHelper API",
        Version = "v1",
        Description = "KitchenHelper uygulamas\u0131 i\u00e7in REST API servisleri",
        Contact = new OpenApiContact
        {
            Name = "KitchenHelper Team"
        }
    });
});

var app = builder.Build();

// Veritaban\u0131 migration'lar\u0131n\u0131 otomatik uygula ve seed et
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // Migration'lar\u0131 uygula
    
    // E\u011fer tarif yoksa seed et
    if (!db.Recipes.Any())
    {
        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "recipes.json");
        if (File.Exists(jsonPath))
        {
            Console.WriteLine("\U0001f4e6 Tarifler yükleniyor...");
            await Kitchenhelper.Web.SeedRecipes.SeedAsync(db, jsonPath);
            Console.WriteLine("\u2705 Tarifler başarıyla yüklendi!");
        }
    }
}

// Swagger'\u0131 sadece development ortam\u0131nda etkinle\u015ftir
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KitchenHelper API v1");
        c.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTPS yönlendirmesini sadece production'da zorunlu tut
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// G\u00fcvenlik header'lar\u0131 (XSS, Clickjacking, MIME sniffing korumas\u0131)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

// CORS middleware'i - routing'den \u00f6nce olmal\u0131
app.UseCors("AllowSpecificOrigins");

// Static files - routing'den \u00d6NCE olmal\u0131
app.UseStaticFiles();

// Serve files from the "uploads" directory at /uploads
var uploadsPath = app.Configuration["UploadsPath"] ?? "uploads";
var uploadsPhysicalPath = Path.Combine(Directory.GetCurrentDirectory(), uploadsPath);
if (Directory.Exists(uploadsPhysicalPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPhysicalPath),
        RequestPath = "/uploads"
    });
}

app.UseRouting();

// Rate limiting middleware
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// API route'lar\u0131
app.MapControllers();

// React build dosyalar\u0131n\u0131 servis et
// app.UseDefaultFiles();

// // React SPA fallback - t\u00fcm route'lar\u0131 index.html'e y\u00f6nlendir
// // API route'lar\u0131ndan sonra, MVC route'lar\u0131ndan \u00f6nce
// app.MapFallbackToFile("index.html");

app.Run();
