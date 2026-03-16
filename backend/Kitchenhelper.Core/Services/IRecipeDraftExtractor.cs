namespace Kitchenhelper.Core.Services;

/// <summary>
/// Extracts structured recipe data from transcript text using LLM.
/// The implementation is model-agnostic (can use Gemini, OpenAI, etc.)
/// </summary>
public interface IRecipeDraftExtractor
{
    /// <summary>
    /// Extracts a structured recipe draft from transcript text.
    /// </summary>
    /// <param name="transcriptText">Raw transcript from ASR</param>
    /// <param name="language">Target language for recipe extraction (tr = Turkish, en = English)</param>
    /// <returns>Valid JSON string conforming to RecipeDraftSchema</returns>
    /// <exception cref="DraftExtractionException">When extraction or validation fails</exception>
    Task<string> ExtractDraftAsync(string transcriptText, string language = "tr");

    /// <summary>
    /// Validates that a JSON string conforms to the RecipeDraftSchema.
    /// </summary>
    /// <param name="json">JSON string to validate</param>
    /// <returns>Validation result with errors if any</returns>
    DraftValidationResult ValidateDraftJson(string json);
}

/// <summary>
/// Result of draft JSON validation
/// </summary>
public class DraftValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public static DraftValidationResult Success() => new() { IsValid = true };
    
    public static DraftValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Exception thrown when draft extraction fails
/// </summary>
public class DraftExtractionException : Exception
{
    public string? RawResponse { get; }
    public List<string> ValidationErrors { get; }

    public DraftExtractionException(string message, string? rawResponse = null, List<string>? validationErrors = null)
        : base(message)
    {
        RawResponse = rawResponse;
        ValidationErrors = validationErrors ?? new();
    }
}
