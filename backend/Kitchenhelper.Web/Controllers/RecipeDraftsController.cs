using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Kitchenhelper.Web.Controllers;

[Route("api/recipe-drafts")]
[ApiController]
[EnableRateLimiting("api")]
public class RecipeDraftsController : ControllerBase
{
    // Guest user ID for unauthenticated access
    private const int GuestUserId = 1;
    
    private readonly IRecipeDraftService _draftService;
    private readonly IRecipePublishService _publishService;
    private readonly IRecipeDraftExtractor _draftExtractor;
    private readonly IConfiguration _configuration;

    public RecipeDraftsController(
        IRecipeDraftService draftService,
        IRecipePublishService publishService,
        IRecipeDraftExtractor draftExtractor,
        IConfiguration configuration)
    {
        _draftService = draftService;
        _publishService = publishService;
        _draftExtractor = draftExtractor;
        _configuration = configuration;
    }

    /// <summary>
    /// GET /api/recipe-drafts/by-import/{importId}
    /// Returns draft for an import with user ownership validation
    /// </summary>
    [HttpGet("by-import/{importId}")]
    public async Task<IActionResult> GetByImportId(int importId)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        var draft = await _draftService.GetByImportIdAsync(importId, userId);
        
        if (draft == null)
            return NotFound(new { message = "Draft bulunamadı" });

        return Ok(new
        {
            draftId = draft.Id,
            importId = draft.ImportId,
            draftJson = draft.DraftJson,
            status = draft.Status.ToString().ToLower(),
            publishedRecipeId = draft.PublishedRecipeId,
            errorMessage = draft.ErrorMessage,
            createdAt = draft.CreatedAt,
            updatedAt = draft.UpdatedAt
        });
    }

    /// <summary>
    /// GET /api/recipe-drafts/{draftId}
    /// Returns draft by ID with user ownership validation
    /// </summary>
    [HttpGet("{draftId}")]
    public async Task<IActionResult> GetById(int draftId)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        var draft = await _draftService.GetByIdAsync(draftId, userId);
        
        if (draft == null)
            return NotFound(new { message = "Draft bulunamadı" });

        return Ok(new
        {
            draftId = draft.Id,
            importId = draft.ImportId,
            draftJson = draft.DraftJson,
            status = draft.Status.ToString().ToLower(),
            publishedRecipeId = draft.PublishedRecipeId,
            errorMessage = draft.ErrorMessage,
            createdAt = draft.CreatedAt,
            updatedAt = draft.UpdatedAt
        });
    }

    /// <summary>
    /// PUT /api/recipe-drafts/{draftId}
    /// Updates draft JSON (for autosave after user edits)
    /// </summary>
    [HttpPut("{draftId}")]
    public async Task<IActionResult> UpdateDraft(int draftId, [FromBody] UpdateDraftRequest request)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        if (string.IsNullOrWhiteSpace(request.DraftJson))
            return BadRequest(new { message = "draftJson zorunludur" });

        // Validate JSON structure
        var validation = _draftExtractor.ValidateDraftJson(request.DraftJson);
        if (!validation.IsValid)
        {
            return BadRequest(new { 
                message = "Geçersiz draft JSON", 
                errors = validation.Errors 
            });
        }

        try
        {
            var draft = await _draftService.UpdateDraftJsonAsync(draftId, userId, request.DraftJson);
            
            return Ok(new
            {
                draftId = draft.Id,
                status = draft.Status.ToString().ToLower(),
                updatedAt = draft.UpdatedAt,
                message = "Draft güncellendi"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/recipe-drafts/{draftId}/publish
    /// Publishes draft to normalized recipe tables
    /// </summary>
    [HttpPost("{draftId}/publish")]
    public async Task<IActionResult> PublishDraft(int draftId)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        try
        {
            var recipe = await _publishService.PublishDraftAsync(draftId, userId);
            
            return Ok(new
            {
                recipeId = recipe.Id,
                title = recipe.Title,
                message = "Tarif başarıyla yayınlandı"
            });
        }
        catch (PublishException ex)
        {
            return BadRequest(new { 
                message = ex.Message, 
                errors = ex.ValidationErrors 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/recipe-drafts/retry-extraction/{importId}
    /// Retries draft extraction for a failed import
    /// </summary>
    [HttpPost("retry-extraction/{importId}")]
    public async Task<IActionResult> RetryExtraction(int importId)
    {
        var userId = GetAuthenticatedUserId() ?? GuestUserId;

        // This would need access to IRecipeImportService to get the import
        // For now, return not implemented
        return StatusCode(501, new { message = "Bu özellik henüz implemente edilmedi" });
    }

    #region Authentication Helper

    private int? GetAuthenticatedUserId()
    {
        // Try cookie first
        var token = Request.Cookies["auth_token"];
        
        // Fallback to Authorization header
        if (string.IsNullOrEmpty(token))
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                token = authHeader.Substring("Bearer ".Length);
            }
        }

        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "");
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst("userId")?.Value;
            
            if (int.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

public class UpdateDraftRequest
{
    public string DraftJson { get; set; } = "";
}
