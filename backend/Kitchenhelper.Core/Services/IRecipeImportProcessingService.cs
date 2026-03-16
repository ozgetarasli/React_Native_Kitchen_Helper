namespace Kitchenhelper.Core.Services;

/// <summary>
/// Orchestrates the complete import processing pipeline
/// </summary>
public interface IRecipeImportProcessingService
{
    /// <summary>
    /// Process a single import through the complete pipeline:
    /// 1. Validate source
    /// 2. Extract audio
    /// 3. Transcribe audio
    /// 4. Update status
    /// </summary>
    /// <param name="importId">The import to process</param>
    Task ProcessImportAsync(int importId);
}
