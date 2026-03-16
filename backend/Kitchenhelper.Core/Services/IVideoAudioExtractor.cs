namespace Kitchenhelper.Core.Services;

/// <summary>
/// Video to audio extraction service interface.
/// Abstracts the extraction library (e.g., FFmpeg)
/// </summary>
public interface IVideoAudioExtractor
{
    /// <summary>
    /// Extracts audio from video file
    /// </summary>
    /// <param name="videoFilePath">Path to input video file</param>
    /// <param name="outputAudioPath">Path where audio should be saved (WAV, 16kHz, mono)</param>
    /// <returns>Duration in seconds</returns>
    Task<int> ExtractAudioAsync(string videoFilePath, string outputAudioPath);
    
    /// <summary>
    /// Gets video metadata without extracting audio
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <returns>Tuple of (durationSeconds, fileSizeBytes)</returns>
    Task<(int durationSeconds, long fileSizeBytes)> GetVideoMetadataAsync(string videoFilePath);
}
