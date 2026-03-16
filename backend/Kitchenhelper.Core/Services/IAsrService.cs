namespace Kitchenhelper.Core.Services;

/// <summary>
/// Automatic Speech Recognition service interface.
/// Abstracts the ASR provider (e.g., Google Gemini, Whisper, etc.)
/// </summary>
public interface IAsrService
{
    /// <summary>
    /// Transcribes audio file to text
    /// </summary>
    /// <param name="audioFilePath">Path to audio file (WAV, 16kHz, mono)</param>
    /// <returns>Plain text transcript</returns>
    Task<string> TranscribeAsync(string audioFilePath);
}
