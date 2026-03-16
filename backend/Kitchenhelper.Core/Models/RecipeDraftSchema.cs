using System.Text.Json.Serialization;

namespace Kitchenhelper.Core.Models;

/// <summary>
/// Strict JSON schema for recipe drafts extracted from transcripts.
/// This is the contract between LLM extraction and the UI.
/// </summary>
public class RecipeDraftSchema
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("servings")]
    public int? Servings { get; set; }

    [JsonPropertyName("prepTimeMin")]
    public int? PrepTimeMin { get; set; }

    [JsonPropertyName("cookTimeMin")]
    public int? CookTimeMin { get; set; }

    [JsonPropertyName("totalTimeMin")]
    public int? TotalTimeMin { get; set; }

    [JsonPropertyName("ingredients")]
    public List<DraftIngredient> Ingredients { get; set; } = new();

    [JsonPropertyName("steps")]
    public List<DraftStep> Steps { get; set; } = new();

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = new();

    [JsonPropertyName("sourceNotes")]
    public SourceNotes? SourceNotes { get; set; }

    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("calories")]
    public double? Calories { get; set; }

    [JsonPropertyName("protein")]
    public double? Protein { get; set; }

    [JsonPropertyName("fat")]
    public double? Fat { get; set; }

    [JsonPropertyName("carbs")]
    public double? Carbs { get; set; }
}

public class DraftIngredient
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>
    /// Flags for uncertain or missing data detected during extraction
    /// </summary>
    [JsonPropertyName("flags")]
    public IngredientFlags? Flags { get; set; }
}

/// <summary>
/// Flags indicating uncertain or missing data for an ingredient
/// </summary>
public class IngredientFlags
{
    /// <summary>
    /// Quantity is uncertain or estimated (e.g., "bir tutam", "yeteri kadar")
    /// </summary>
    [JsonPropertyName("uncertainQuantity")]
    public bool UncertainQuantity { get; set; }

    /// <summary>
    /// Unit is missing or unclear
    /// </summary>
    [JsonPropertyName("missingUnit")]
    public bool MissingUnit { get; set; }

    /// <summary>
    /// Original text that was hard to parse
    /// </summary>
    [JsonPropertyName("originalText")]
    public string? OriginalText { get; set; }
}

public class DraftStep
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("timeHintMin")]
    public int? TimeHintMin { get; set; }

    /// <summary>
    /// Flags for uncertain or missing data detected during extraction
    /// </summary>
    [JsonPropertyName("flags")]
    public StepFlags? Flags { get; set; }
}

/// <summary>
/// Flags indicating uncertain or missing data for a step
/// </summary>
public class StepFlags
{
    /// <summary>
    /// Time is missing or estimated
    /// </summary>
    [JsonPropertyName("missingTime")]
    public bool MissingTime { get; set; }

    /// <summary>
    /// Temperature is uncertain or missing
    /// </summary>
    [JsonPropertyName("uncertainTemperature")]
    public bool UncertainTemperature { get; set; }

    /// <summary>
    /// Step text seems incomplete
    /// </summary>
    [JsonPropertyName("incomplete")]
    public bool Incomplete { get; set; }
}

public class SourceNotes
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "tr";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.0;
}

/// <summary>
/// Validation warnings for the draft (not errors, but things user should check)
/// </summary>
public class DraftValidationWarnings
{
    [JsonPropertyName("missingServings")]
    public bool MissingServings { get; set; }

    [JsonPropertyName("missingPrepTime")]
    public bool MissingPrepTime { get; set; }

    [JsonPropertyName("missingCookTime")]
    public bool MissingCookTime { get; set; }

    [JsonPropertyName("emptyIngredients")]
    public bool EmptyIngredients { get; set; }

    [JsonPropertyName("emptySteps")]
    public bool EmptySteps { get; set; }

    [JsonPropertyName("ingredientsWithUncertainQuantity")]
    public int IngredientsWithUncertainQuantity { get; set; }

    [JsonPropertyName("stepsWithMissingTime")]
    public int StepsWithMissingTime { get; set; }

    [JsonPropertyName("lowConfidence")]
    public bool LowConfidence { get; set; }

    /// <summary>
    /// Summary messages for the user
    /// </summary>
    [JsonPropertyName("messages")]
    public List<string> Messages { get; set; } = new();

    public bool HasWarnings => MissingServings || MissingPrepTime || MissingCookTime ||
        EmptyIngredients || EmptySteps || IngredientsWithUncertainQuantity > 0 ||
        StepsWithMissingTime > 0 || LowConfidence;
}
