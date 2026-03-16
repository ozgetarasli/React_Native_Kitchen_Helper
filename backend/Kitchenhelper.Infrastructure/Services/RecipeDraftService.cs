using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Service for managing recipe drafts with user isolation
/// </summary>
public class RecipeDraftService : IRecipeDraftService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecipeDraftService> _logger;

    public RecipeDraftService(AppDbContext db, ILogger<RecipeDraftService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RecipeDraft> CreateAsync(int userId, int importId, string draftJson)
    {
        // Verify import belongs to user
        var import = await _db.RecipeImports
            .FirstOrDefaultAsync(i => i.Id == importId && i.UserId == userId);

        if (import == null)
        {
            throw new InvalidOperationException("Import bulunamadı veya erişim yetkiniz yok");
        }

        // Check if draft already exists for this import
        var existingDraft = await _db.RecipeDrafts
            .FirstOrDefaultAsync(d => d.ImportId == importId);

        if (existingDraft != null)
        {
            // Update existing draft
            existingDraft.DraftJson = draftJson;
            existingDraft.Status = DraftStatus.Ready;
            existingDraft.ErrorMessage = null;
            existingDraft.UpdatedAt = DateTime.UtcNow;
            
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Updated existing draft {DraftId} for import {ImportId}", 
                existingDraft.Id, importId);
            
            return existingDraft;
        }

        // Create new draft
        var draft = new RecipeDraft
        {
            UserId = userId,
            ImportId = importId,
            DraftJson = draftJson,
            Status = DraftStatus.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RecipeDrafts.Add(draft);
        await _db.SaveChangesAsync();

        // Update import with draft ID
        import.DraftId = draft.Id;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created draft {DraftId} for import {ImportId}, user {UserId}", 
            draft.Id, importId, userId);

        return draft;
    }

    public async Task<RecipeDraft?> GetByIdAsync(int draftId, int userId)
    {
        return await _db.RecipeDrafts
            .Include(d => d.Import)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId);
    }

    public async Task<RecipeDraft?> GetByImportIdAsync(int importId, int userId)
    {
        return await _db.RecipeDrafts
            .Include(d => d.Import)
            .FirstOrDefaultAsync(d => d.ImportId == importId && d.UserId == userId);
    }

    public async Task<RecipeDraft> UpdateDraftJsonAsync(int draftId, int userId, string draftJson)
    {
        var draft = await GetByIdAsync(draftId, userId);
        
        if (draft == null)
        {
            throw new InvalidOperationException("Draft bulunamadı veya erişim yetkiniz yok");
        }

        if (draft.Status == DraftStatus.Published)
        {
            throw new InvalidOperationException("Yayınlanmış draft güncellenemez");
        }

        draft.DraftJson = draftJson;
        draft.Status = DraftStatus.Edited;
        draft.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Updated draft {DraftId} JSON, status changed to Edited", draftId);

        return draft;
    }

    public async Task MarkAsPublishedAsync(int draftId, int recipeId)
    {
        var draft = await _db.RecipeDrafts.FindAsync(draftId);
        
        if (draft == null)
        {
            throw new InvalidOperationException("Draft bulunamadı");
        }

        draft.Status = DraftStatus.Published;
        draft.PublishedRecipeId = recipeId;
        draft.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Draft {DraftId} published as Recipe {RecipeId}", draftId, recipeId);
    }

    public async Task MarkAsFailedAsync(int draftId, string errorMessage)
    {
        var draft = await _db.RecipeDrafts.FindAsync(draftId);
        
        if (draft == null)
        {
            throw new InvalidOperationException("Draft bulunamadı");
        }

        draft.ErrorMessage = errorMessage;
        draft.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogWarning("Draft {DraftId} marked as failed: {Error}", draftId, errorMessage);
    }
}
