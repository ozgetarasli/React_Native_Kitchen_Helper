using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// URL-based video analysis and download service.
/// Detects video platforms, checks transcript availability, and handles downloads.
/// Uses yt-dlp for supported platforms.
/// </summary>
public class UrlVideoService : IUrlVideoService
{
    private readonly ILogger<UrlVideoService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _ytDlpPath;
    private readonly IVideoDownloadSettings _settings;
    private readonly IUserAgentRotator _userAgentRotator;
    private readonly IRequestThrottler _requestThrottler;
    private readonly AsyncRetryPolicy _retryPolicy;

    // Platform detection patterns
    private static readonly (Regex Pattern, VideoPlatform Platform)[] PlatformPatterns = 
    {
        (new Regex(@"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/shorts/)", RegexOptions.IgnoreCase), VideoPlatform.YouTube),
        (new Regex(@"vimeo\.com/", RegexOptions.IgnoreCase), VideoPlatform.Vimeo),
        (new Regex(@"instagram\.com/(?:p|reel|tv)/", RegexOptions.IgnoreCase), VideoPlatform.Instagram),
        (new Regex(@"tiktok\.com/", RegexOptions.IgnoreCase), VideoPlatform.TikTok),
        (new Regex(@"facebook\.com/.*video|fb\.watch/", RegexOptions.IgnoreCase), VideoPlatform.Facebook),
        (new Regex(@"twitter\.com/.*/status/|x\.com/.*/status/", RegexOptions.IgnoreCase), VideoPlatform.Twitter),
        (new Regex(@"dailymotion\.com/video/", RegexOptions.IgnoreCase), VideoPlatform.Dailymotion),
    };

    // Platforms that support transcript/caption extraction
    private static readonly HashSet<VideoPlatform> TranscriptSupportedPlatforms = new()
    {
        VideoPlatform.YouTube,
        VideoPlatform.Vimeo
    };

    // Platforms that yt-dlp can download from
    private static readonly HashSet<VideoPlatform> DownloadablePlatforms = new()
    {
        VideoPlatform.YouTube,
        VideoPlatform.Vimeo,
        VideoPlatform.Instagram,
        VideoPlatform.TikTok,
        VideoPlatform.Facebook,
        VideoPlatform.Twitter,
        VideoPlatform.Dailymotion,
        VideoPlatform.Direct
    };

    public UrlVideoService(
        ILogger<UrlVideoService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IVideoDownloadSettings settings,
        IUserAgentRotator userAgentRotator,
        IRequestThrottler requestThrottler)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _settings = settings;
        _userAgentRotator = userAgentRotator;
        _requestThrottler = requestThrottler;
        _ytDlpPath = configuration["YtDlpPath"] ?? "yt-dlp";
        
        // Configure Polly retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                retryCount: _settings.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(_settings.RetryBaseDelaySeconds, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s for {Url}",
                        retryCount, _settings.MaxRetryAttempts, timeSpan.TotalSeconds, context["url"]);
                });
    }

    public async Task<UrlAnalysisResult> AnalyzeUrlAsync(string url)
    {
        _logger.LogInformation("Analyzing URL: {Url}", url);

        var result = new UrlAnalysisResult();

        // 1. URL erişilebilir mi?
        try
        {
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, url),
                HttpCompletionOption.ResponseHeadersRead);

            result.HttpStatusCode = (int)response.StatusCode;
            result.IsAccessible = response.IsSuccessStatusCode;

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "Bu URL'ye erişim engellendi (403 Forbidden)",
                    HttpStatusCode.NotFound => "Video bulunamadı (404 Not Found)",
                    HttpStatusCode.Unauthorized => "Bu video için yetkilendirme gerekli",
                    _ => $"URL'ye erişilemedi (HTTP {(int)response.StatusCode})"
                };

                // 403/404 durumlarında direkt video yolu varsa platform'a özel kontrol yap
                if (response.StatusCode == HttpStatusCode.Forbidden || 
                    response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Platform tespit et ve yt-dlp ile tekrar dene
                    result.Platform = DetectPlatform(url);
                    
                    if (result.Platform != VideoPlatform.Unknown && result.Platform != VideoPlatform.Direct)
                    {
                        // Platform destekleniyorsa yt-dlp ile erişilebilirliği kontrol et
                        var ytDlpCheck = await CheckYtDlpAvailabilityAsync(url);
                        if (ytDlpCheck.isAvailable)
                        {
                            result.IsAccessible = true;
                            result.IsDownloadable = true;
                            result.VideoTitle = ytDlpCheck.title;
                            result.DurationSeconds = ytDlpCheck.duration;
                            result.VideoDescription = ytDlpCheck.description;
                            result.HasRecipeInDescription = ContainsRecipeContent(ytDlpCheck.description);
                            result.ErrorMessage = null;
                        }
                    }
                }

                if (!result.IsAccessible)
                {
                    result.FailureReason = result.ErrorMessage;
                    result.UserAction = "Lütfen videoyu indirip dosya olarak yükleyin";
                    return result;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed for URL: {Url}", url);
            
            // HTTP hatası olsa bile platform destekleniyorsa yt-dlp dene
            result.Platform = DetectPlatform(url);
            
            if (result.Platform != VideoPlatform.Unknown && result.Platform != VideoPlatform.Direct)
            {
                var ytDlpCheck = await CheckYtDlpAvailabilityAsync(url);
                if (ytDlpCheck.isAvailable)
                {
                    result.IsAccessible = true;
                    result.IsDownloadable = true;
                    result.VideoTitle = ytDlpCheck.title;
                    result.DurationSeconds = ytDlpCheck.duration;
                    result.VideoDescription = ytDlpCheck.description;
                    result.HasRecipeInDescription = ContainsRecipeContent(ytDlpCheck.description);
                }
                else
                {
                    result.IsAccessible = false;
                    result.ErrorMessage = "Video URL'sine erişilemiyor";
                    result.FailureReason = ex.Message;
                    result.UserAction = "Lütfen videoyu indirip dosya olarak yükleyin";
                    return result;
                }
            }
            else
            {
                result.IsAccessible = false;
                result.ErrorMessage = "Video URL'sine erişilemiyor";
                result.FailureReason = ex.Message;
                result.UserAction = "Lütfen videoyu indirip dosya olarak yükleyin";
                return result;
            }
        }
        catch (TaskCanceledException)
        {
            result.IsAccessible = false;
            result.ErrorMessage = "URL isteği zaman aşımına uğradı";
            result.FailureReason = "Timeout";
            result.UserAction = "Lütfen videoyu indirip dosya olarak yükleyin";
            return result;
        }

        // 2. Platform tespit et
        if (result.Platform == VideoPlatform.Unknown)
        {
            result.Platform = DetectPlatform(url);
        }

        // 3. Transcript API mevcut mu?
        result.HasTranscriptApi = TranscriptSupportedPlatforms.Contains(result.Platform);

        // 4. İndirilebilir mi?
        if (!result.IsDownloadable)
        {
            result.IsDownloadable = DownloadablePlatforms.Contains(result.Platform);

            // Direct URL için Content-Type kontrolü
            if (result.Platform == VideoPlatform.Unknown || result.Platform == VideoPlatform.Direct)
            {
                try
                {
                    var response = await _httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, url),
                        HttpCompletionOption.ResponseHeadersRead);

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                    result.IsDownloadable = contentType.StartsWith("video/") || contentType.StartsWith("audio/");
                    
                    if (result.IsDownloadable)
                    {
                        result.Platform = VideoPlatform.Direct;
                    }
                }
                catch
                {
                    result.IsDownloadable = false;
                }
            }
        }
        
        // 5. Platform destekliyorsa ve henüz description alınmadıysa, yt-dlp ile metadata al
        // Bu özellikle TikTok, Instagram gibi platformlar için önemli
        if (result.IsDownloadable && 
            string.IsNullOrEmpty(result.VideoDescription) && 
            result.Platform != VideoPlatform.Direct &&
            result.Platform != VideoPlatform.Unknown)
        {
            _logger.LogInformation("Fetching video metadata via yt-dlp for {Platform}", result.Platform);
            var ytDlpCheck = await CheckYtDlpAvailabilityAsync(url);
            if (ytDlpCheck.isAvailable)
            {
                result.VideoTitle = ytDlpCheck.title;
                result.DurationSeconds = ytDlpCheck.duration;
                result.VideoDescription = ytDlpCheck.description;
                result.HasRecipeInDescription = ContainsRecipeContent(ytDlpCheck.description);
                
                _logger.LogInformation(
                    "Video metadata retrieved: Title={Title}, Duration={Duration}s, HasRecipeInDesc={HasRecipe}",
                    result.VideoTitle, result.DurationSeconds, result.HasRecipeInDescription);
            }
        }

        // 6. İndirilemiyorsa hata mesajı
        if (!result.IsDownloadable && !result.HasTranscriptApi)
        {
            result.FailureReason = result.Platform switch
            {
                VideoPlatform.Unknown => "Bu URL'den video tespit edilemedi",
                VideoPlatform.Instagram => "Instagram videoları kısıtlı erişimli olabilir",
                VideoPlatform.TikTok => "TikTok videoları kısıtlı erişimli olabilir",
                VideoPlatform.Facebook => "Facebook videoları genellikle gizli erişimli",
                _ => $"{result.Platform} platformundan video indirilemedi"
            };
            result.UserAction = "Lütfen videoyu indirip dosya olarak yükleyin";
        }

        _logger.LogInformation(
            "URL analysis complete: Platform={Platform}, Accessible={Accessible}, HasTranscript={HasTranscript}, Downloadable={Downloadable}",
            result.Platform, result.IsAccessible, result.HasTranscriptApi, result.IsDownloadable);

        return result;
    }

    public async Task<string?> TryGetPlatformTranscriptAsync(string url)
    {
        var platform = DetectPlatform(url);

        if (!TranscriptSupportedPlatforms.Contains(platform))
        {
            _logger.LogInformation("Platform {Platform} does not support transcript API", platform);
            return null;
        }

        try
        {
            return platform switch
            {
                VideoPlatform.YouTube => await TryGetYouTubeTranscriptAsync(url),
                VideoPlatform.Vimeo => await TryGetVimeoTranscriptAsync(url),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get platform transcript for {Url}", url);
            return null;
        }
    }

    public async Task<string?> TryGetVideoDescriptionAsync(string url)
    {
        _logger.LogInformation("Getting video description for {Url}", url);
        
        try
        {
            var ytDlpCheck = await CheckYtDlpAvailabilityAsync(url);
            return ytDlpCheck.description;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get video description for {Url}", url);
            return null;
        }
    }

    public async Task<bool> DownloadVideoAsync(string url, string outputPath)
    {
        _logger.LogInformation("Downloading video from {Url} to {OutputPath}", url, outputPath);

        var platform = DetectPlatform(url);

        // Direct URL için basit HTTP download
        if (platform == VideoPlatform.Direct)
        {
            return await DownloadDirectUrlAsync(url, outputPath);
        }

        // Diğer platformlar için yt-dlp kullan
        return await DownloadWithYtDlpAsync(url, outputPath);
    }

    public async Task<bool> DownloadAudioOnlyAsync(string url, string outputPath)
    {
        _logger.LogInformation("Downloading audio only from {Url} to {OutputPath} (FAST MODE)", url, outputPath);

        var platform = DetectPlatform(url);
        
        try
        {
            // Apply throttling before download
            var canProceed = await _requestThrottler.WaitForRequestAsync(platform);
            if (!canProceed)
            {
                _logger.LogWarning("Audio download request throttled for platform {Platform}", platform);
                return false;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Build audio-only download arguments
            var args = new StringBuilder();
            var platformSettings = _settings.GetPlatformSettings(platform);
            var useImpersonation = platform == VideoPlatform.TikTok || platform == VideoPlatform.Instagram;

            if (!useImpersonation)
            {
                var userAgent = _userAgentRotator.GetUserAgentForPlatform(platform);
                args.Append($"--user-agent \"{userAgent}\" ");
            }

            if (_settings.UseCookies)
            {
                if (!string.IsNullOrEmpty(_settings.CookiesFromBrowser))
                    args.Append($"--cookies-from-browser {_settings.CookiesFromBrowser} ");
                else if (!string.IsNullOrEmpty(_settings.CookiesPath) && File.Exists(_settings.CookiesPath))
                    args.Append($"--cookies \"{_settings.CookiesPath}\" ");
            }

            if (!string.IsNullOrEmpty(_settings.ProxyUrl))
                args.Append($"--proxy \"{_settings.ProxyUrl}\" ");

            if (!string.IsNullOrEmpty(platformSettings.AdditionalYtDlpArgs))
                args.Append($"{platformSettings.AdditionalYtDlpArgs} ");

            args.Append("--no-warnings --no-check-certificate --geo-bypass ");

            if (useImpersonation)
                args.Append("--impersonate chrome ");

            // AUDIO ONLY - Much faster download!
            args.Append("-x "); // Extract audio
            args.Append("--audio-format wav "); // Convert to WAV for Whisper
            args.Append("--audio-quality 0 "); // Best quality
            args.Append($"-o \"{outputPath}\" \"{url}\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = args.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogDebug("yt-dlp audio-only arguments: {Args}", args.ToString());

            using var process = new Process { StartInfo = startInfo };
            var errorOutput = new StringBuilder();

            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };

            process.Start();
            process.BeginErrorReadLine();

            // Audio download should be much faster - 2 minute timeout
            var completed = await Task.Run(() => process.WaitForExit(120000));

            _requestThrottler.RecordRequest(platform, null, completed && process.ExitCode == 0);

            if (!completed)
            {
                try { process.Kill(); } catch { }
                _logger.LogError("yt-dlp audio download timed out for {Url}", url);
                return false;
            }

            var error = errorOutput.ToString();
            if (IsBlockedOrRateLimited(error))
            {
                _logger.LogError("Platform {Platform} blocked during audio download: {Error}", platform, error);
                return false;
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("yt-dlp audio download failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                return false;
            }

            // Check for output file (yt-dlp may add .wav extension)
            var possibleFiles = new[]
            {
                outputPath,
                outputPath + ".wav",
                Path.ChangeExtension(outputPath, ".wav"),
                Path.ChangeExtension(outputPath, ".m4a"),
                Path.ChangeExtension(outputPath, ".mp3")
            };

            foreach (var file in possibleFiles)
            {
                if (File.Exists(file))
                {
                    _logger.LogInformation("Audio downloaded successfully: {FilePath} ({Size} KB)", 
                        file, new FileInfo(file).Length / 1024);
                    
                    // Rename to expected path if different
                    if (file != outputPath && File.Exists(file))
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                        File.Move(file, outputPath);
                    }
                    return true;
                }
            }

            _logger.LogError("Audio file not found after download at {OutputPath}", outputPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio-only download failed for {Url}", url);
            return false;
        }
    }

    #region Private Methods

    /// <summary>
    /// Checks if video description contains recipe-related content
    /// </summary>
    private bool ContainsRecipeContent(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var lowerDesc = description.ToLowerInvariant();

        // Turkish recipe keywords
        var turkishKeywords = new[]
        {
            "malzemeler", "malzeme", "tarif", "yapılışı", "yapilisi", "hazırlanışı", "hazirlani",
            "pişirme", "pisirme", "yemek", "tatlı", "tatli", "çorba", "corba", "salata",
            "porsiyon", "kaşık", "kasik", "bardak", "gram", "kilo", "litre", "adet",
            "su bardağı", "su bardagi", "çay kaşığı", "cay kasigi", "yemek kaşığı",
            "un", "şeker", "seker", "tuz", "yağ", "yag", "süt", "sut", "yumurta"
        };

        // English recipe keywords
        var englishKeywords = new[]
        {
            "ingredients", "recipe", "instructions", "directions", "how to make",
            "cooking", "baking", "servings", "serves", "prep time", "cook time",
            "tablespoon", "teaspoon", "cup", "cups", "ounce", "pound", "gram",
            "flour", "sugar", "salt", "butter", "oil", "milk", "eggs"
        };

        // Check for keyword matches
        var turkishMatches = turkishKeywords.Count(k => lowerDesc.Contains(k));
        var englishMatches = englishKeywords.Count(k => lowerDesc.Contains(k));

        // If at least 3 keywords found, likely contains recipe
        return (turkishMatches >= 3) || (englishMatches >= 3) || (turkishMatches + englishMatches >= 4);
    }

    private VideoPlatform DetectPlatform(string url)
    {
        foreach (var (pattern, platform) in PlatformPatterns)
        {
            if (pattern.IsMatch(url))
                return platform;
        }

        // Check if it's a direct video URL
        if (url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
        {
            return VideoPlatform.Direct;
        }

        return VideoPlatform.Unknown;
    }

    private async Task<(bool isAvailable, string? title, int? duration, string? description)> CheckYtDlpAvailabilityAsync(string url)
    {
        var platform = DetectPlatform(url);
        
        try
        {
            // Apply throttling before request
            var canProceed = await _requestThrottler.WaitForRequestAsync(platform);
            if (!canProceed)
            {
                _logger.LogWarning("Request throttled for platform {Platform}", platform);
                return (false, null, null, null);
            }
            
            var args = BuildYtDlpArguments(url, platform, metadataOnly: true);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var errorOutput = new StringBuilder();
            
            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            var completed = await Task.Run(() => process.WaitForExit(60000));
            
            // Record the request
            _requestThrottler.RecordRequest(platform, null, completed && process.ExitCode == 0);
            
            if (!completed)
            {
                try { process.Kill(); } catch { }
                return (false, null, null, null);
            }
            
            // Check for common error patterns
            var error = errorOutput.ToString();
            if (IsBlockedOrRateLimited(error))
            {
                _logger.LogWarning("Platform {Platform} blocked or rate limited: {Error}", platform, error);
                return (false, null, null, null);
            }
            
            if (process.ExitCode != 0)
            {
                _logger.LogWarning("yt-dlp metadata failed (exit {ExitCode}) for {Url}: {Error}",
                    process.ExitCode, url, error);
                return (false, null, null, null);
            }

            var json = output.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return (false, null, null, null);

            // yt-dlp birden fazla JSON objesi dönebilir, sadece ilkini al
            var lines = json.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var firstJsonLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("{"));
            
            if (string.IsNullOrWhiteSpace(firstJsonLine))
                return (false, null, null, null);

            var doc = JsonDocument.Parse(firstJsonLine);
            var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
            var duration = doc.RootElement.TryGetProperty("duration", out var d) ? (int?)d.GetDouble() : null;
            var description = doc.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() : null;

            return (true, title, duration, description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "yt-dlp check failed for {Url}", url);
            return (false, null, null, null);
        }
    }
    
    /// <summary>
    /// Builds yt-dlp arguments with platform-specific settings, cookies, and User-Agent.
    /// </summary>
    private string BuildYtDlpArguments(string url, VideoPlatform platform, bool metadataOnly = false, string? outputPath = null)
    {
        var args = new StringBuilder();
        var platformSettings = _settings.GetPlatformSettings(platform);
        var useImpersonation = platform == VideoPlatform.TikTok || platform == VideoPlatform.Instagram;
        
        // Add User-Agent (skip when impersonating to avoid conflicting headers)
        if (!useImpersonation)
        {
            var userAgent = _userAgentRotator.GetUserAgentForPlatform(platform);
            args.Append($"--user-agent \"{userAgent}\" ");
        }
        
        // Add cookies if configured and platform requires/supports them
        if (_settings.UseCookies)
        {
            if (!string.IsNullOrEmpty(_settings.CookiesFromBrowser))
            {
                args.Append($"--cookies-from-browser {_settings.CookiesFromBrowser} ");
            }
            else if (!string.IsNullOrEmpty(_settings.CookiesPath) && File.Exists(_settings.CookiesPath))
            {
                args.Append($"--cookies \"{_settings.CookiesPath}\" ");
            }
        }
        
        // Add proxy if configured
        if (!string.IsNullOrEmpty(_settings.ProxyUrl))
        {
            args.Append($"--proxy \"{_settings.ProxyUrl}\" ");
        }
        
        // Add platform-specific arguments
        if (!string.IsNullOrEmpty(platformSettings.AdditionalYtDlpArgs))
        {
            args.Append($"{platformSettings.AdditionalYtDlpArgs} ");
        }
        
        // Common arguments to avoid detection
        args.Append("--no-warnings ");
        args.Append("--no-check-certificate ");
        args.Append("--geo-bypass ");
        
        // Use browser impersonation for platforms with strict bot detection
        // Requires curl_cffi to be installed: pip install curl_cffi
        if (useImpersonation)
        {
            args.Append("--impersonate chrome ");
            _logger.LogDebug("Using Chrome impersonation for {Platform}", platform);
        }
        
        // Add rate limiting to appear more human-like
        args.Append("--sleep-requests 1 ");
        args.Append("--sleep-interval 2 ");
        args.Append("--max-sleep-interval 5 ");
        
        if (metadataOnly)
        {
            args.Append($"--dump-json --no-download \"{url}\"");
        }
        else if (!string.IsNullOrEmpty(outputPath))
        {
            args.Append($"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" ");
            args.Append($"--merge-output-format mp4 ");
            args.Append($"-o \"{outputPath}\" \"{url}\"");
        }
        
        return args.ToString();
    }
    
    /// <summary>
    /// Checks if the error indicates IP block or rate limiting.
    /// </summary>
    private bool IsBlockedOrRateLimited(string error)
    {
        if (string.IsNullOrEmpty(error))
            return false;
            
        var lowerError = error.ToLowerInvariant();
        
        return lowerError.Contains("ip address is blocked") ||
               lowerError.Contains("rate limit") ||
               lowerError.Contains("too many requests") ||
               lowerError.Contains("429") ||
               lowerError.Contains("access denied") ||
               lowerError.Contains("login required") ||
               lowerError.Contains("private video") ||
               lowerError.Contains("sign in to confirm") ||
               lowerError.Contains("captcha");
    }

    private async Task<string?> TryGetYouTubeTranscriptAsync(string url)
    {
        // YouTube video ID'sini çıkar
        var videoId = ExtractYouTubeVideoId(url);
        if (string.IsNullOrEmpty(videoId))
            return null;

        _logger.LogInformation("Attempting to download subtitles for video {VideoId}...", videoId);

        try
        {
            // yt-dlp ile subtitle indir - timeout 15 saniye (subtitle olmazsa hızlıca devam et)
            var tempFile = Path.GetTempFileName();
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"--write-auto-sub --sub-lang tr,en --skip-download --sub-format vtt --socket-timeout 10 -o \"{tempFile}\" \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            // Subtitle için 15 saniye yeterli, yoksa devam et
            var completed = await Task.Run(() => process.WaitForExit(15000));
            
            if (!completed)
            {
                _logger.LogWarning("Subtitle download timed out after 15 seconds for video {VideoId}", videoId);
                try { process.Kill(); } catch { }
                return null;
            }

            // VTT dosyasını bul ve oku
            var dir = Path.GetDirectoryName(tempFile)!;
            var baseName = Path.GetFileNameWithoutExtension(tempFile);
            var vttFiles = Directory.GetFiles(dir, $"{baseName}*.vtt");
            
            if (vttFiles.Length == 0)
            {
                _logger.LogInformation("No subtitle files found for video {VideoId}", videoId);
                try { File.Delete(tempFile); } catch { }
                return null;
            }

            _logger.LogInformation("Found {Count} subtitle file(s) for video {VideoId}", vttFiles.Length, videoId);
            var vttContent = await File.ReadAllTextAsync(vttFiles[0]);
            
            // VTT'yi düz metne çevir
            var transcript = ConvertVttToPlainText(vttContent);
            
            // Temizlik
            foreach (var f in vttFiles) try { File.Delete(f); } catch { }
            try { File.Delete(tempFile); } catch { }

            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get YouTube transcript for {VideoId}", videoId);
            return null;
        }
    }

    private Task<string?> TryGetVimeoTranscriptAsync(string url)
    {
        // Vimeo transcript API - MVP için basit implementasyon
        // İleride Vimeo API ile genişletilebilir
        _logger.LogInformation("Vimeo transcript API not implemented in MVP");
        return Task.FromResult<string?>(null);
    }

    private async Task<bool> DownloadDirectUrlAsync(string url, string outputPath)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download direct URL: {Url}", url);
            return false;
        }
    }

    private async Task<bool> DownloadWithYtDlpAsync(string url, string outputPath)
    {
        var platform = DetectPlatform(url);
        
        try
        {
            _logger.LogInformation("Starting yt-dlp download for {Url} to {OutputPath} (Platform: {Platform})", 
                url, outputPath, platform);
            
            // Apply throttling before download
            var canProceed = await _requestThrottler.WaitForRequestAsync(platform);
            if (!canProceed)
            {
                _logger.LogWarning("Download request throttled for platform {Platform}", platform);
                return false;
            }
            
            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Önceki kilitli dosyaları temizle (WinError 32 önleme)
            var tempPath = Path.ChangeExtension(outputPath, ".temp.mp4");
            try
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Could not clean up old files: {Error}", ex.Message);
                // Yeni dosya adı kullan
                outputPath = Path.Combine(dir!, $"source_{DateTime.Now.Ticks}.mp4");
            }

            // Build yt-dlp arguments with all anti-ban measures
            var args = BuildYtDlpArguments(url, platform, metadataOnly: false, outputPath: outputPath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogDebug("yt-dlp arguments: {Args}", args);

            using var process = new Process { StartInfo = startInfo };
            var errorOutput = new StringBuilder();
            var stdOutput = new StringBuilder();
            
            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdOutput.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // 10 dakika timeout (büyük dosyalar için)
            var completed = await Task.Run(() => process.WaitForExit(600000));
            
            // Record the request
            _requestThrottler.RecordRequest(platform, null, completed && process.ExitCode == 0);

            if (!completed)
            {
                try { process.Kill(); } catch { }
                _logger.LogError("yt-dlp download timed out for {Url}", url);
                return false;
            }

            _logger.LogInformation("yt-dlp exit code: {ExitCode}", process.ExitCode);
            
            // Check for block/rate limit errors
            var error = errorOutput.ToString();
            if (IsBlockedOrRateLimited(error))
            {
                _logger.LogError("Platform {Platform} blocked or rate limited during download: {Error}", 
                    platform, error);
                return false;
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("yt-dlp failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return false;
            }

            // Check for various possible output files
            var possibleFiles = new[]
            {
                outputPath,
                Path.ChangeExtension(outputPath, ".mp4"),
                Path.ChangeExtension(outputPath, ".webm"),
                Path.ChangeExtension(outputPath, ".mkv")
            };

            foreach (var file in possibleFiles)
            {
                if (File.Exists(file))
                {
                    _logger.LogInformation("Downloaded file found at: {FilePath}", file);
                    
                    // If the file is not at the expected path, rename it
                    if (file != outputPath && File.Exists(file))
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                        File.Move(file, outputPath);
                    }
                    return true;
                }
            }

            // Check directory for any downloaded files
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir);
                _logger.LogWarning("No expected file found. Files in directory: {Files}", string.Join(", ", files));
            }

            _logger.LogError("yt-dlp completed but output file not found at {OutputPath}", outputPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp download failed for {Url}", url);
            return false;
        }
    }

    private string? ExtractYouTubeVideoId(string url)
    {
        var patterns = new[]
        {
            @"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/shorts/)([a-zA-Z0-9_-]{11})",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    private string ConvertVttToPlainText(string vttContent)
    {
        var lines = vttContent.Split('\n');
        var textLines = new List<string>();
        var seenTexts = new HashSet<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip VTT headers, timestamps, and empty lines
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("WEBVTT") ||
                trimmed.StartsWith("Kind:") ||
                trimmed.StartsWith("Language:") ||
                trimmed.Contains("-->") ||
                Regex.IsMatch(trimmed, @"^\d+$"))
            {
                continue;
            }

            // Remove VTT formatting tags
            var cleanText = Regex.Replace(trimmed, @"<[^>]+>", "");
            cleanText = Regex.Replace(cleanText, @"&nbsp;", " ");
            
            // Skip duplicates (auto-captions often repeat)
            if (!string.IsNullOrWhiteSpace(cleanText) && seenTexts.Add(cleanText))
            {
                textLines.Add(cleanText);
            }
        }

        return string.Join(" ", textLines);
    }

    #endregion
}
