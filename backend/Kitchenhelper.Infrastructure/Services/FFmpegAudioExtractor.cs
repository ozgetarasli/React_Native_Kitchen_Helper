using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Video to audio extraction using FFmpeg.
/// Requires FFmpeg to be installed on the system.
/// </summary>
public class FFmpegAudioExtractor : IVideoAudioExtractor
{
    private readonly ILogger<FFmpegAudioExtractor> _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FFmpegAudioExtractor(ILogger<FFmpegAudioExtractor> logger, IConfiguration configuration)
    {
        _logger = logger;
        _ffmpegPath = configuration["FFmpegPath"] ?? "ffmpeg"; // Use 'ffmpeg' from PATH by default

        // Resolve ffprobe path: prefer explicit config, else try alongside ffmpeg, else fallback to 'ffprobe'
        var configuredFfprobe = configuration["FFprobePath"];
        if (!string.IsNullOrWhiteSpace(configuredFfprobe))
        {
            _ffprobePath = configuredFfprobe;
        }
        else
        {
            // If ffmpeg is an absolute path, try using ffprobe from the same directory
            try
            {
                if (!string.Equals(_ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(_ffmpegPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
                        _ffprobePath = File.Exists(candidate) ? candidate : "ffprobe";
                    }
                    else
                    {
                        _ffprobePath = "ffprobe";
                    }
                }
                else
                {
                    _ffprobePath = "ffprobe";
                }
            }
            catch
            {
                _ffprobePath = "ffprobe";
            }
        }
    }

    public async Task<int> ExtractAudioAsync(string videoFilePath, string outputAudioPath)
    {
        _logger.LogInformation("Extracting audio from {VideoPath} to {AudioPath}", videoFilePath, outputAudioPath);

        // FFmpeg command to extract audio as WAV, 16kHz, mono
        // -i input.mp4 -vn -acodec pcm_s16le -ar 16000 -ac 1 output.wav
        var arguments = $"-i \"{videoFilePath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 -y \"{outputAudioPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var errorOutput = new StringBuilder();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorOutput.AppendLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg failed: {Error}", errorOutput.ToString());
            throw new InvalidOperationException($"FFmpeg failed to extract audio: {errorOutput}");
        }

        // Get duration from extracted audio file
        var duration = await GetAudioDurationAsync(outputAudioPath);

        _logger.LogInformation("Audio extraction successful, duration: {Duration}s", duration);
        return duration;
    }

    public async Task<(int durationSeconds, long fileSizeBytes)> GetVideoMetadataAsync(string videoFilePath)
    {
        _logger.LogInformation("Getting metadata for {VideoPath}", videoFilePath);

        // Use ffprobe to get metadata
        // ffprobe -v error -show_entries format=duration,size -of default=noprint_wrappers=1 input.mp4
        var arguments = $"-v error -show_entries format=duration,size -of default=noprint_wrappers=1:nokey=1 \"{videoFilePath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath, // Comes with FFmpeg
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        _logger.LogInformation("ffprobe output: {Output}", output);
        _logger.LogInformation("ffprobe stderr: {Error}", errorOutput);

        if (process.ExitCode != 0)
        {
            _logger.LogError("ffprobe failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
            throw new InvalidOperationException($"Failed to get video metadata using ffprobe: {errorOutput}");
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            _logger.LogError("Unexpected ffprobe output format. Output: {Output}", output);
            throw new InvalidOperationException($"Unexpected ffprobe output format. Got {lines.Length} lines, expected 2");
        }

        if (!double.TryParse(lines[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var durationDouble))
        {
            _logger.LogError("Failed to parse duration from: {DurationString}", lines[0]);
            throw new InvalidOperationException($"Failed to parse duration from: {lines[0]}");
        }

        if (!long.TryParse(lines[1].Trim(), out var fileSize))
        {
            _logger.LogError("Failed to parse file size from: {FileSizeString}", lines[1]);
            throw new InvalidOperationException($"Failed to parse file size from: {lines[1]}");
        }

        var duration = (int)durationDouble;

        return (duration, fileSize);
    }

    private async Task<int> GetAudioDurationAsync(string audioFilePath)
    {
        var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioFilePath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (int)double.Parse(output.Trim(), CultureInfo.InvariantCulture);
    }
}
