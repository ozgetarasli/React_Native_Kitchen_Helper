using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

/// <summary>
/// Service for publishing drafts to normalized recipe tables
/// </summary>
public interface IRecipePublishService
{
    /// <summary>
    /// Publishes a draft to the Recipes table with normalized ingredients.
    /// </summary>
    /// <param name="draftId">Draft to publish</param>
    /// <param name="userId">User performing the action</param>
    /// <returns>Created Recipe</returns>
    /// <exception cref="PublishException">When validation or publish fails</exception>
    Task<Recipe> PublishDraftAsync(int draftId, int userId);
}

/// <summary>
/// Exception thrown when publishing fails
/// </summary>
public class PublishException : Exception
{
    public List<string> ValidationErrors { get; }

    public PublishException(string message, List<string>? validationErrors = null)
        : base(message)
    {
        ValidationErrors = validationErrors ?? new();
    }
}
