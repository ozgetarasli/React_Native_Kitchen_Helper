using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Infrastructure.Services;

public class RecipeImportService : IRecipeImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecipeImportService> _logger;

    public RecipeImportService(AppDbContext db, ILogger<RecipeImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RecipeImport?> GetAsync(int id, int userId)
    {
        return await _db.RecipeImports
            .Include(ri => ri.Recipe)
            .FirstOrDefaultAsync(ri => ri.Id == id && ri.UserId == userId);
    }

    public async Task<RecipeImport?> GetByIdAsync(int id)
    {
        return await _db.RecipeImports
            .Include(ri => ri.Recipe)
            .Include(ri => ri.User)
            .FirstOrDefaultAsync(ri => ri.Id == id);
    }

    public async Task<List<RecipeImport>> GetAllByUserAsync(int userId)
    {
        return await _db.RecipeImports
            .Include(ri => ri.Recipe)
            .Where(ri => ri.UserId == userId)
            .OrderByDescending(ri => ri.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> CreateFromUrlAsync(int userId, string videoUrl, string recipeLanguage = "tr")
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            throw new ArgumentException("Video URL cannot be empty", nameof(videoUrl));

        var recipeImport = new RecipeImport
        {
            UserId = userId,
            SourceType = SourceType.Url,
            SourceUrl = videoUrl.Trim(),
            Status = ImportStatus.Queued,
            RecipeLanguage = recipeLanguage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RecipeImports.Add(recipeImport);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created URL import {ImportId} for user {UserId} (lang: {Lang})", recipeImport.Id, userId, recipeLanguage);
        return recipeImport.Id;
    }

    public async Task<int> CreateFromFileAsync(int userId, string videoFilePath, string recipeLanguage = "tr")
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be empty", nameof(videoFilePath));

        var recipeImport = new RecipeImport
        {
            UserId = userId,
            SourceType = SourceType.File,
            SourceFilePath = videoFilePath.Trim(),
            Status = ImportStatus.Queued,
            RecipeLanguage = recipeLanguage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RecipeImports.Add(recipeImport);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created file import {ImportId} for user {UserId} (lang: {Lang})", recipeImport.Id, userId, recipeLanguage);
        return recipeImport.Id;
    }

    public async Task UpdateStatusAsync(int id, ImportStatus status, string? errorMessage = null)
    {
        var recipeImport = await _db.RecipeImports.FindAsync(id);
        if (recipeImport == null)
            throw new InvalidOperationException($"RecipeImport with id {id} not found");

        recipeImport.Status = status;
        recipeImport.ErrorMessage = errorMessage;
        recipeImport.UpdatedAt = DateTime.UtcNow;

        if (status == ImportStatus.Done || status == ImportStatus.Failed)
        {
            recipeImport.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<RecipeImport>> GetQueuedImportsAsync(int limit = 10)
    {
        return await _db.RecipeImports
            .Where(ri => ri.Status == ImportStatus.Queued)
            .OrderBy(ri => ri.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task UpdateAsync(RecipeImport import)
    {
        import.UpdatedAt = DateTime.UtcNow;
        _db.RecipeImports.Update(import);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Atomically tries to acquire an import for processing.
    /// Uses optimistic concurrency control to prevent duplicate processing.
    /// </summary>
    public async Task<bool> TryAcquireForProcessingAsync(int importId)
    {
        try
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE RecipeImports 
                  SET Status = {0}, UpdatedAt = {1}
                  WHERE Id = {2} AND Status = {3}",
                (int)ImportStatus.Processing,
                DateTime.UtcNow,
                importId,
                (int)ImportStatus.Queued
            );

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire import {ImportId} for processing", importId);
            return false;
        }
    }
}
