using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

/// <summary>
/// Service for managing recipe drafts (CRUD + user isolation)
/// </summary>
public interface IRecipeDraftService
{
    /// <summary>
    /// Creates a new draft from extracted JSON
    /// </summary>
    Task<RecipeDraft> CreateAsync(int userId, int importId, string draftJson);

    /// <summary>
    /// Gets a draft by ID with user ownership validation
    /// </summary>
    Task<RecipeDraft?> GetByIdAsync(int draftId, int userId);

    /// <summary>
    /// Gets draft by import ID with user ownership validation
    /// </summary>
    Task<RecipeDraft?> GetByImportIdAsync(int importId, int userId);

    /// <summary>
    /// Updates draft JSON (for autosave)
    /// </summary>
    Task<RecipeDraft> UpdateDraftJsonAsync(int draftId, int userId, string draftJson);

    /// <summary>
    /// Marks draft as published with the created recipe ID
    /// </summary>
    Task MarkAsPublishedAsync(int draftId, int recipeId);

    /// <summary>
    /// Marks draft as failed with error message
    /// </summary>
    Task MarkAsFailedAsync(int draftId, string errorMessage);
}
