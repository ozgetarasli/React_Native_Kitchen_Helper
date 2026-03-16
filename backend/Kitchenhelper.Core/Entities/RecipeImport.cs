using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Core.Entities;

public enum ImportStatus
{
    Queued,
    Processing,
    Done,
    Failed
}

public enum SourceType
{
    File,
    Url
}

/// <summary>
/// Indicates the source of the transcript used for recipe extraction
/// </summary>
public enum TranscriptSource
{
    /// <summary>Not yet determined</summary>
    Unknown = 0,
    /// <summary>Extracted from video description (yt-dlp metadata)</summary>
    VideoDescription = 1,
    /// <summary>Extracted from platform subtitles/captions (yt-dlp)</summary>
    PlatformSubtitles = 2,
    /// <summary>Generated via Whisper ASR from audio</summary>
    WhisperASR = 3,
    /// <summary>Combined description/subtitles with Whisper ASR</summary>
    Combined = 4
}

public class RecipeImport
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public SourceType SourceType { get; set; }

    [StringLength(500)]
    public string? SourceUrl { get; set; }

    [StringLength(500)]
    public string? SourceFilePath { get; set; }

    [StringLength(500)]
    public string? AudioPath { get; set; }

    [StringLength(500)]
    public string? TranscriptPath { get; set; }

    public ImportStatus Status { get; set; } = ImportStatus.Queued;

    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    /// <summary>
    /// Link to the extracted draft (set after successful extraction)
    /// </summary>
    public int? DraftId { get; set; }
    public RecipeDraft? Draft { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Operational constraints
    public long? FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Indicates which method was used to extract the transcript
    /// </summary>
    public TranscriptSource TranscriptSource { get; set; } = TranscriptSource.Unknown;

    /// <summary>
    /// Language code for recipe extraction (tr = Turkish, en = English)
    /// </summary>
    [StringLength(10)]
    public string RecipeLanguage { get; set; } = "tr";
}
