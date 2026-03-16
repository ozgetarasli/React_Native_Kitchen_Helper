using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

public interface IRecipeImportService
{
    Task<RecipeImport?> GetAsync(int id, int userId);
    Task<List<RecipeImport>> GetAllByUserAsync(int userId);
    Task<int> CreateFromUrlAsync(int userId, string videoUrl, string recipeLanguage = "tr");
    Task<int> CreateFromFileAsync(int userId, string videoFilePath, string recipeLanguage = "tr");
    Task UpdateStatusAsync(int id, ImportStatus status, string? errorMessage = null);
    
    // New methods for background processing
    Task<List<RecipeImport>> GetQueuedImportsAsync(int limit = 10);
    Task<RecipeImport?> GetByIdAsync(int id);
    Task UpdateAsync(RecipeImport import);
    Task<bool> TryAcquireForProcessingAsync(int importId);
}
