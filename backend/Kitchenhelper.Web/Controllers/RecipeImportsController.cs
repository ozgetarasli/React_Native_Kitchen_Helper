using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Kitchenhelper.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("api")]
public class RecipeImportsController : ControllerBase
{
    private readonly IRecipeImportService _service;
    private readonly IConfiguration _configuration;
    
    // Guest user ID for unauthenticated users
    private const int GuestUserId = 1;

    public RecipeImportsController(IRecipeImportService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    // GET api/recipeimports - Get all imports for current user
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        var imports = await _service.GetAllByUserAsync(userId);
        
        var result = imports.Select(i => new
        {
            id = i.Id,
            sourceType = i.SourceType.ToString().ToLower(),
            sourceUrl = i.SourceUrl,
            sourceFilePath = i.SourceFilePath,
            status = i.Status.ToString().ToLower(),
            errorMessage = i.ErrorMessage,
            recipeId = i.RecipeId,
            recipeTitle = i.Recipe?.Title,
            audioPath = i.AudioPath,
            transcriptPath = i.TranscriptPath,
            transcriptSource = i.TranscriptSource.ToString(),
            transcriptSourceLabel = GetTranscriptSourceLabel(i.TranscriptSource),
            createdAt = i.CreatedAt,
            updatedAt = i.UpdatedAt,
            completedAt = i.CompletedAt,
            durationSeconds = i.DurationSeconds,
            fileSizeBytes = i.FileSizeBytes,
            draftId = i.DraftId
        }).ToList();

        return Ok(result);
    }

    // GET api/recipeimports/{id} - Get specific import
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        var import = await _service.GetAsync(id, userId);
        if (import == null)
            return NotFound(new { message = "Import bulunamadı" });

        var result = new
        {
            id = import.Id,
            sourceType = import.SourceType.ToString().ToLower(),
            sourceUrl = import.SourceUrl,
            sourceFilePath = import.SourceFilePath,
            status = import.Status.ToString().ToLower(),
            errorMessage = import.ErrorMessage,
            recipeId = import.RecipeId,
            recipeTitle = import.Recipe?.Title,
            audioPath = import.AudioPath,
            transcriptPath = import.TranscriptPath,
            transcriptSource = import.TranscriptSource.ToString(),
            transcriptSourceLabel = GetTranscriptSourceLabel(import.TranscriptSource),
            createdAt = import.CreatedAt,
            updatedAt = import.UpdatedAt,
            completedAt = import.CompletedAt,
            durationSeconds = import.DurationSeconds,
            fileSizeBytes = import.FileSizeBytes,
            draftId = import.DraftId
        };

        return Ok(result);
    }

    // GET api/recipeimports/{id}/transcript - Download transcript.txt
    [HttpGet("{id}/transcript")]
    public async Task<IActionResult> GetTranscript(int id)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        var import = await _service.GetAsync(id, userId);
        if (import == null)
            return NotFound(new { message = "Import bulunamadı" });

        if (string.IsNullOrWhiteSpace(import.TranscriptPath) || !System.IO.File.Exists(import.TranscriptPath))
            return NotFound(new { message = "Transkript dosyası bulunamadı" });

        var fileName = System.IO.Path.GetFileName(import.TranscriptPath);
        return PhysicalFile(import.TranscriptPath, "text/plain", fileName);
    }

    // POST api/recipeimports/url - Create import from URL
    [HttpPost("url")]
    public async Task<IActionResult> CreateFromUrl([FromBody] CreateFromUrlRequest request)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        if (string.IsNullOrWhiteSpace(request.VideoUrl))
            return BadRequest(new { message = "Video URL gereklidir" });

        try
        {
            var importId = await _service.CreateFromUrlAsync(userId, request.VideoUrl, request.RecipeLanguage);
            return StatusCode(201, new { id = importId, message = "Import başarıyla oluşturuldu" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/recipeimports/file - Create import from file
    [HttpPost("file")]
    public async Task<IActionResult> CreateFromFile([FromBody] CreateFromFileRequest request)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        if (string.IsNullOrWhiteSpace(request.VideoFilePath))
            return BadRequest(new { message = "Video dosya yolu gereklidir" });

        try
        {
            var importId = await _service.CreateFromFileAsync(userId, request.VideoFilePath, request.RecipeLanguage);
            return StatusCode(201, new { id = importId, message = "Import başarıyla oluşturuldu" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/recipeimports/upload - Upload video file
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Dosya seçiniz" });

        // Dosya uzantısı kontrolü - sadece video dosyalarına izin ver
        var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm", ".flv", ".wmv", ".m4v" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest(new { message = "Sadece video dosyaları yüklenebilir (mp4, avi, mov, mkv, webm, flv, wmv, m4v)." });

        // Dosya boyutu kontrolü - max 500MB
        const long maxFileSize = 500L * 1024 * 1024;
        if (file.Length > maxFileSize)
            return BadRequest(new { message = "Dosya boyutu 500MB'dan büyük olamaz." });

        try
        {
            var uploadsPath = _configuration["UploadsPath"] ?? "uploads";
            var importsDir = Path.Combine(uploadsPath, "imports");
            
            // Create unique directory for this import
            var importDirName = Guid.NewGuid().ToString();
            var importDir = Path.Combine(importsDir, importDirName);
            Directory.CreateDirectory(importDir);

            // Save file with sanitized extension
            var savedFilename = $"source{fileExtension}";
            var filePath = Path.Combine(importDir, savedFilename);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path for use in create-from-file request
            var relativePath = Path.Combine(importDir, savedFilename).Replace("\\", "/");

            return Ok(new { filePath = relativePath, message = "Dosya yüklendi" });
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Video upload error: {ex.Message}");
            return StatusCode(500, new { message = "Dosya yüklenirken beklenmeyen bir hata oluştu." });
        }
    }

    // PATCH api/recipeimports/{id}/status - Update import status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        // Verify that the import belongs to the current user
        var import = await _service.GetAsync(id, userId);
        if (import == null)
            return NotFound(new { message = "Import bulunamadı" });

        if (!Enum.TryParse<ImportStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Geçersiz status değeri. Geçerli değerler: queued, processing, done, failed" });

        try
        {
            await _service.UpdateStatusAsync(id, status, request.ErrorMessage);
            return Ok(new { message = "Status güncellendi" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // Helper method to extract userId from JWT token in cookie
    private int? GetAuthenticatedUserId()
    {
        var token = Request.Cookies["auth_token"];
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "");

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "userId");
            
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                return userId;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetTranscriptSourceLabel(TranscriptSource source)
    {
        return source switch
        {
            TranscriptSource.VideoDescription => "Video Açıklaması (yt-dlp)",
            TranscriptSource.PlatformSubtitles => "Platform Altyazıları (yt-dlp)",
            TranscriptSource.WhisperASR => "Ses İşleme (Whisper ASR)",
            TranscriptSource.Combined => "Açıklama + Altyazı + Whisper (Birleştirilmiş)",
            _ => "Bilinmiyor"
        };
    }
}

// Request models
public class CreateFromUrlRequest
{
    public string VideoUrl { get; set; } = "";
    public string RecipeLanguage { get; set; } = "tr";
}

public class CreateFromFileRequest
{
    public string VideoFilePath { get; set; } = "";
    public string RecipeLanguage { get; set; } = "tr";
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
}
