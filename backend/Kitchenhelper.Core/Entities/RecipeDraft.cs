using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Core.Entities;

public enum DraftStatus
{
    Ready,      // Draft extracted, waiting for user review
    Edited,     // User has made edits
    Published   // Converted to Recipe
}

/// <summary>
/// Stores the structured JSON draft extracted from a transcript.
/// This is an intermediate state before publishing to the normalized Recipe tables.
/// </summary>
public class RecipeDraft
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ImportId { get; set; }
    public RecipeImport Import { get; set; } = null!;

    /// <summary>
    /// The structured recipe JSON following the defined schema.
    /// Stored as TEXT to avoid DB bloat with large recipes.
    /// </summary>
    [Required]
    public string DraftJson { get; set; } = "{}";

    public DraftStatus Status { get; set; } = DraftStatus.Ready;

    /// <summary>
    /// The published Recipe ID (set after successful publish)
    /// </summary>
    public int? PublishedRecipeId { get; set; }
    public Recipe? PublishedRecipe { get; set; }

    /// <summary>
    /// Error message if extraction or validation failed
    /// </summary>
    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
