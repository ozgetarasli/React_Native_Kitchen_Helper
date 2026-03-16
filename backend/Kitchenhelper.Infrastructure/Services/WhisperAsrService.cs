using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// ASR implementation using local OpenAI Whisper.
/// Runs Whisper via Python subprocess for speech-to-text transcription.
/// Supports chunking for long audio files and post-processing for cleaner output.
/// </summary>
public class WhisperAsrService : IAsrService
{
    private readonly ILogger<WhisperAsrService> _logger;
    private readonly string _whisperModel;
    private readonly string _pythonPath;
    private readonly string _whisperScriptPath;
    
    // Chunk settings
    private const int ChunkDurationSeconds = 600; // 10 minutes per chunk
    private const int ChunkOverlapSeconds = 5;    // 5 second overlap for context
    
    // Filler words to remove (Turkish and English)
    private static readonly string[] FillerPatterns = new[]
    {
        // Turkish fillers
        @"\b(eee+|ee+)\b",
        @"\b(şe+y)\b",
        @"\b(hani)\b",
        @"\b(işte)\b",
        @"\b(yani)\b",
        @"\b(aslında)\s+(?=aslında)",  // repeated "aslında"
        @"\b(falan)\b",
        @"\b(filan)\b",
        @"\b(bilmem)\b",
        @"\b(nasıl denir)\b",
        @"\b(nasıl desem)\b",
        // English fillers
        @"\b(uh+|uhm+|um+|umm+)\b",
        @"\b(er+|erm+)\b",
        @"\b(like)\s+(?=like)",  // repeated "like"
        @"\b(you know)\b",
        @"\b(I mean)\b",
        // Common transcription artifacts
        @"\[.*?\]",  // [music], [laughter], etc.
        @"\(.*?\)",  // (inaudible), etc.
    };

    public WhisperAsrService(
        ILogger<WhisperAsrService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        // Configuration from appsettings.json
        _whisperModel = configuration["Whisper:Model"] ?? "base";
        _pythonPath = configuration["Whisper:PythonPath"] ?? "python";
        
        // Script path - in the same directory as the web project
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _whisperScriptPath = configuration["Whisper:ScriptPath"] 
            ?? Path.Combine(basePath, "..", "..", "..", "..", "whisper_transcribe.py");
    }

    public async Task<string> TranscribeAsync(string audioFilePath)
    {
        _logger.LogInformation("Transcribing audio file with Whisper: {AudioPath}", audioFilePath);

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found", audioFilePath);

        try
        {
            // Check audio duration to decide if chunking is needed
            var duration = await GetAudioDurationAsync(audioFilePath);
            _logger.LogInformation("Audio duration: {Duration} seconds", duration);
            
            string rawTranscript;
            
            if (duration > ChunkDurationSeconds)
            {
                _logger.LogInformation("Long audio detected ({Duration}s), using chunked transcription", duration);
                rawTranscript = await TranscribeChunkedAsync(audioFilePath, duration);
            }
            else
            {
                rawTranscript = await TranscribeSingleAsync(audioFilePath);
            }
            
            // Post-process the transcript
            var cleanedTranscript = PostProcessTranscript(rawTranscript);
            
            _logger.LogInformation("Transcription complete. Raw: {RawLen} chars, Cleaned: {CleanLen} chars",
                rawTranscript.Length, cleanedTranscript.Length);
                
            return cleanedTranscript;
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not TimeoutException)
        {
            _logger.LogError(ex, "Failed to transcribe audio using Whisper");
            throw new InvalidOperationException("Whisper transcription failed", ex);
        }
    }
    
    /// <summary>
    /// Gets audio duration using FFprobe
    /// </summary>
    private async Task<int> GetAudioDurationAsync(string audioPath)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = new Process { StartInfo = processInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit(30000));
            
            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                return (int)Math.Ceiling(duration);
            }
            
            // Default to max if we can't determine duration
            return ChunkDurationSeconds * 2;
        }
        catch
        {
            // If FFprobe fails, assume it's a long audio and chunk anyway
            return ChunkDurationSeconds * 2;
        }
    }
    
    /// <summary>
    /// Transcribes long audio by splitting into chunks
    /// </summary>
    private async Task<string> TranscribeChunkedAsync(string audioFilePath, int totalDuration)
    {
        var transcriptParts = new List<string>();
        var chunkDir = Path.Combine(Path.GetDirectoryName(audioFilePath)!, "chunks");
        Directory.CreateDirectory(chunkDir);
        
        try
        {
            var chunkIndex = 0;
            var currentStart = 0;
            
            while (currentStart < totalDuration)
            {
                var chunkPath = Path.Combine(chunkDir, $"chunk_{chunkIndex:D3}.wav");
                var chunkDuration = Math.Min(ChunkDurationSeconds, totalDuration - currentStart);
                
                _logger.LogInformation("Processing chunk {Index}: {Start}s - {End}s", 
                    chunkIndex, currentStart, currentStart + chunkDuration);
                
                // Extract chunk using FFmpeg
                await ExtractAudioChunkAsync(audioFilePath, chunkPath, currentStart, chunkDuration + ChunkOverlapSeconds);
                
                // Transcribe chunk
                var chunkTranscript = await TranscribeSingleAsync(chunkPath);
                
                if (!string.IsNullOrWhiteSpace(chunkTranscript))
                {
                    transcriptParts.Add(chunkTranscript.Trim());
                }
                
                // Clean up chunk file
                try { File.Delete(chunkPath); } catch { /* ignore */ }
                
                currentStart += ChunkDurationSeconds;
                chunkIndex++;
            }
            
            // Merge transcripts, removing potential overlaps
            return MergeChunkTranscripts(transcriptParts);
        }
        finally
        {
            // Clean up chunk directory
            try { Directory.Delete(chunkDir, true); } catch { /* ignore */ }
        }
    }
    
    /// <summary>
    /// Extracts a chunk of audio using FFmpeg
    /// </summary>
    private async Task ExtractAudioChunkAsync(string inputPath, string outputPath, int startSeconds, int durationSeconds)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -i \"{inputPath}\" -ss {startSeconds} -t {durationSeconds} -ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = processInfo };
        process.Start();
        
        var completed = await Task.Run(() => process.WaitForExit(60000));
        
        if (!completed || process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to extract audio chunk at {startSeconds}s");
        }
    }
    
    /// <summary>
    /// Merges chunk transcripts, attempting to remove overlapping content
    /// </summary>
    private string MergeChunkTranscripts(List<string> parts)
    {
        if (parts.Count == 0) return "";
        if (parts.Count == 1) return parts[0];
        
        var merged = new StringBuilder();
        
        for (int i = 0; i < parts.Count; i++)
        {
            var current = parts[i];
            
            if (i == 0)
            {
                merged.Append(current);
                continue;
            }
            
            // Try to find overlap with previous part
            var previousEnd = parts[i - 1];
            var overlapRemoved = RemoveOverlap(previousEnd, current);
            
            merged.Append(" ");
            merged.Append(overlapRemoved);
        }
        
        return merged.ToString().Trim();
    }
    
    /// <summary>
    /// Attempts to remove overlapping content between two transcript parts
    /// </summary>
    private string RemoveOverlap(string previous, string current)
    {
        // Get last few words of previous transcript
        var previousWords = previous.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentWords = current.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (previousWords.Length < 5 || currentWords.Length < 5)
            return current;
        
        // Look for overlap in first 10 words of current
        var lastWords = previousWords.TakeLast(10).ToArray();
        var firstWords = currentWords.Take(15).ToArray();
        
        for (int overlapSize = Math.Min(10, firstWords.Length); overlapSize >= 3; overlapSize--)
        {
            var potentialOverlap = string.Join(" ", firstWords.Take(overlapSize));
            var matchInPrevious = string.Join(" ", lastWords.TakeLast(overlapSize));
            
            // Fuzzy match - at least 80% similar
            if (IsSimilar(matchInPrevious, potentialOverlap, 0.8))
            {
                return string.Join(" ", currentWords.Skip(overlapSize));
            }
        }
        
        return current;
    }
    
    /// <summary>
    /// Simple similarity check
    /// </summary>
    private bool IsSimilar(string a, string b, double threshold)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        
        if (a == b) return true;
        
        var longer = a.Length > b.Length ? a : b;
        var shorter = a.Length > b.Length ? b : a;
        
        if (longer.Length == 0) return true;
        
        // Simple character-level similarity
        int matches = 0;
        for (int i = 0; i < shorter.Length; i++)
        {
            if (i < longer.Length && shorter[i] == longer[i])
                matches++;
        }
        
        return (double)matches / longer.Length >= threshold;
    }

    /// <summary>
    /// Transcribes a single audio file (no chunking)
    /// </summary>
    private async Task<string> TranscribeSingleAsync(string audioFilePath)
    {
        // Output file for transcript
        var outputPath = Path.ChangeExtension(audioFilePath, ".txt");

        // Run Whisper via command line
        var processInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{_whisperScriptPath}\" \"{audioFilePath}\" \"{outputPath}\" \"{_whisperModel}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _logger.LogDebug("Running Whisper: {FileName} {Arguments}", processInfo.FileName, processInfo.Arguments);

        using var process = new Process { StartInfo = processInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
                outputBuilder.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
                errorBuilder.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for process to complete (max 5 minutes for long audio)
        var completed = await Task.Run(() => process.WaitForExit(300000));

        if (!completed)
        {
            process.Kill();
            throw new TimeoutException("Whisper transcription timed out after 5 minutes");
        }

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("Whisper process failed with exit code {ExitCode}: {Error}", 
                process.ExitCode, error);
            throw new InvalidOperationException($"Whisper transcription failed: {error}");
        }

        // Read transcript from output file
        if (!File.Exists(outputPath))
        {
            // Try to get transcript from stdout if file wasn't created
            var stdout = outputBuilder.ToString().Trim();
            if (!string.IsNullOrEmpty(stdout))
            {
                _logger.LogInformation("Transcription successful from stdout, length: {Length} characters", stdout.Length);
                return stdout;
            }
            throw new InvalidOperationException("Whisper did not produce output file or transcript");
        }

        var transcript = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);

        // Clean up output file
        try { File.Delete(outputPath); } catch { /* ignore */ }

        if (string.IsNullOrWhiteSpace(transcript))
            throw new InvalidOperationException("Empty transcript result from Whisper");

        return transcript.Trim();
    }
    
    /// <summary>
    /// Post-processes transcript to remove filler words and clean up text
    /// </summary>
    private string PostProcessTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return transcript;
            
        var cleaned = transcript;
        
        // Remove filler words and patterns
        foreach (var pattern in FillerPatterns)
        {
            cleaned = Regex.Replace(cleaned, pattern, " ", RegexOptions.IgnoreCase);
        }
        
        // Remove repeated words (e.g., "ve ve ve" -> "ve")
        cleaned = Regex.Replace(cleaned, @"\b(\w+)\s+\1\b", "$1", RegexOptions.IgnoreCase);
        
        // Remove triple+ repeated words
        cleaned = Regex.Replace(cleaned, @"\b(\w+)(\s+\1){2,}\b", "$1", RegexOptions.IgnoreCase);
        
        // Clean up multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
        
        // Clean up multiple punctuation
        cleaned = Regex.Replace(cleaned, @"\.{2,}", ".");
        cleaned = Regex.Replace(cleaned, @",{2,}", ",");
        
        // Fix spacing around punctuation
        cleaned = Regex.Replace(cleaned, @"\s+([.,!?;:])", "$1");
        cleaned = Regex.Replace(cleaned, @"([.,!?;:])(\w)", "$1 $2");
        
        // Trim each line and rejoin
        var lines = cleaned.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l));
        
        return string.Join("\n", lines).Trim();
    }
}
