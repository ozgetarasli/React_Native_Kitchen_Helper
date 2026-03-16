namespace Kitchenhelper.Core.Services;

/// <summary>
/// URL-based video analysis and download service interface.
/// Handles platform detection, transcript availability, and video downloading.
/// </summary>
public interface IUrlVideoService
{
    /// <summary>
    /// Analyzes a video URL to determine processing options
    /// </summary>
    /// <param name="url">Video URL to analyze</param>
    /// <returns>Analysis result with available options</returns>
    Task<UrlAnalysisResult> AnalyzeUrlAsync(string url);

    /// <summary>
    /// Tries to extract transcript directly from platform (YouTube captions, etc.)
    /// </summary>
    /// <param name="url">Video URL</param>
    /// <returns>Transcript text if available, null otherwise</returns>
    Task<string?> TryGetPlatformTranscriptAsync(string url);

    /// <summary>
    /// Tries to get video description that may contain recipe/ingredients
    /// </summary>
    /// <param name="url">Video URL</param>
    /// <returns>Video description if available, null otherwise</returns>
    Task<string?> TryGetVideoDescriptionAsync(string url);

    /// <summary>
    /// Downloads video/audio from URL to local file
    /// </summary>
    /// <param name="url">Video URL</param>
    /// <param name="outputPath">Path to save the downloaded file</param>
    /// <returns>True if download successful</returns>
    Task<bool> DownloadVideoAsync(string url, string outputPath);

    /// <summary>
    /// Downloads audio only from URL (much faster than full video - ~5-20MB vs 50-200MB).
    /// Used for Whisper ASR transcription without needing the video file.
    /// </summary>
    /// <param name="url">Video URL</param>
    /// <param name="outputPath">Path to save the audio file (wav format)</param>
    /// <returns>True if download successful</returns>
    Task<bool> DownloadAudioOnlyAsync(string url, string outputPath);
}

/// <summary>
/// Result of URL analysis
/// </summary>
public class UrlAnalysisResult
{
    public bool IsAccessible { get; set; }
    public int HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detected platform (YouTube, Vimeo, Instagram, TikTok, Direct, Unknown)
    /// </summary>
    public VideoPlatform Platform { get; set; }

    /// <summary>
    /// Whether platform has transcript/caption API available
    /// </summary>
    public bool HasTranscriptApi { get; set; }

    /// <summary>
    /// Whether video can be downloaded directly
    /// </summary>
    public bool IsDownloadable { get; set; }

    /// <summary>
    /// Reason why URL cannot be processed (for failed cases)
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Suggested action for user
    /// </summary>
    public string? UserAction { get; set; }

    /// <summary>
    /// Video title if detected
    /// </summary>
    public string? VideoTitle { get; set; }

    /// <summary>
    /// Video description (may contain recipe/ingredients)
    /// </summary>
    public string? VideoDescription { get; set; }

    /// <summary>
    /// Whether description likely contains recipe content
    /// </summary>
    public bool HasRecipeInDescription { get; set; }

    /// <summary>
    /// Estimated duration in seconds if available
    /// </summary>
    public int? DurationSeconds { get; set; }
}

public enum VideoPlatform
{
    Unknown,
    Direct,      // Direct video file URL
    YouTube,
    Vimeo,
    Instagram,
    TikTok,
    Facebook,
    Twitter,
    Dailymotion
}
