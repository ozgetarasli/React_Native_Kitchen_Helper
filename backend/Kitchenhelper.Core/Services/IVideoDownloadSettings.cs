namespace Kitchenhelper.Core.Services;

/// <summary>
/// Configuration settings for video downloading and social media access.
/// Includes rate limiting, throttling, and anti-ban measures.
/// </summary>
public interface IVideoDownloadSettings
{
    /// <summary>
    /// Minimum delay between requests to the same platform (in milliseconds).
    /// </summary>
    int MinRequestDelayMs { get; }
    
    /// <summary>
    /// Maximum delay between requests to the same platform (in milliseconds).
    /// </summary>
    int MaxRequestDelayMs { get; }
    
    /// <summary>
    /// Path to cookies file for yt-dlp authentication.
    /// </summary>
    string? CookiesPath { get; }
    
    /// <summary>
    /// Browser to extract cookies from (chrome, firefox, edge, opera, brave, vivaldi, safari).
    /// </summary>
    string? CookiesFromBrowser { get; }
    
    /// <summary>
    /// Whether to use cookies for authentication.
    /// </summary>
    bool UseCookies { get; }
    
    /// <summary>
    /// Proxy URL for requests (optional).
    /// Format: protocol://[user:pass@]host:port
    /// </summary>
    string? ProxyUrl { get; }
    
    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// </summary>
    int MaxRetryAttempts { get; }
    
    /// <summary>
    /// Base delay for exponential backoff retry (in seconds).
    /// </summary>
    int RetryBaseDelaySeconds { get; }
    
    /// <summary>
    /// Maximum concurrent downloads allowed.
    /// </summary>
    int MaxConcurrentDownloads { get; }
    
    /// <summary>
    /// Maximum downloads per hour per user.
    /// </summary>
    int MaxDownloadsPerHour { get; }
    
    /// <summary>
    /// Maximum downloads per day per user.
    /// </summary>
    int MaxDownloadsPerDay { get; }
    
    /// <summary>
    /// Platform-specific throttle settings.
    /// </summary>
    PlatformThrottleSettings GetPlatformSettings(VideoPlatform platform);
}

/// <summary>
/// Platform-specific throttle and access settings.
/// </summary>
public class PlatformThrottleSettings
{
    /// <summary>
    /// Minimum delay between requests (in milliseconds).
    /// </summary>
    public int MinDelayMs { get; set; } = 3000;
    
    /// <summary>
    /// Maximum delay between requests (in milliseconds).
    /// </summary>
    public int MaxDelayMs { get; set; } = 8000;
    
    /// <summary>
    /// Whether this platform is likely to block or rate limit.
    /// </summary>
    public bool IsHighRiskPlatform { get; set; }
    
    /// <summary>
    /// Whether to require cookies for this platform.
    /// </summary>
    public bool RequiresCookies { get; set; }
    
    /// <summary>
    /// Additional yt-dlp arguments for this platform.
    /// </summary>
    public string? AdditionalYtDlpArgs { get; set; }
}

/// <summary>
/// Default implementation of IVideoDownloadSettings.
/// </summary>
public class VideoDownloadSettings : IVideoDownloadSettings
{
    public int MinRequestDelayMs { get; set; } = 3000;
    public int MaxRequestDelayMs { get; set; } = 10000;
    public string? CookiesPath { get; set; }
    public string? CookiesFromBrowser { get; set; }
    public bool UseCookies { get; set; } = false;
    public string? ProxyUrl { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 2;
    public int MaxConcurrentDownloads { get; set; } = 2;
    public int MaxDownloadsPerHour { get; set; } = 10;
    public int MaxDownloadsPerDay { get; set; } = 50;
    
    /// <summary>
    /// Platform-specific settings with defaults.
    /// </summary>
    public Dictionary<VideoPlatform, PlatformThrottleSettings> PlatformSettings { get; set; } = new()
    {
        [VideoPlatform.YouTube] = new PlatformThrottleSettings
        {
            MinDelayMs = 1000,
            MaxDelayMs = 3000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        },
        [VideoPlatform.Instagram] = new PlatformThrottleSettings
        {
            MinDelayMs = 5000,
            MaxDelayMs = 15000,
            IsHighRiskPlatform = true,
            RequiresCookies = true,
            AdditionalYtDlpArgs = "--no-check-certificate"
        },
        [VideoPlatform.TikTok] = new PlatformThrottleSettings
        {
            MinDelayMs = 5000,
            MaxDelayMs = 15000,
            IsHighRiskPlatform = true,
            RequiresCookies = true,
            AdditionalYtDlpArgs = "--no-check-certificate"
        },
        [VideoPlatform.Facebook] = new PlatformThrottleSettings
        {
            MinDelayMs = 3000,
            MaxDelayMs = 8000,
            IsHighRiskPlatform = true,
            RequiresCookies = true
        },
        [VideoPlatform.Twitter] = new PlatformThrottleSettings
        {
            MinDelayMs = 2000,
            MaxDelayMs = 5000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        },
        [VideoPlatform.Vimeo] = new PlatformThrottleSettings
        {
            MinDelayMs = 1000,
            MaxDelayMs = 3000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        },
        [VideoPlatform.Dailymotion] = new PlatformThrottleSettings
        {
            MinDelayMs = 1000,
            MaxDelayMs = 3000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        },
        [VideoPlatform.Direct] = new PlatformThrottleSettings
        {
            MinDelayMs = 500,
            MaxDelayMs = 1000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        },
        [VideoPlatform.Unknown] = new PlatformThrottleSettings
        {
            MinDelayMs = 2000,
            MaxDelayMs = 5000,
            IsHighRiskPlatform = false,
            RequiresCookies = false
        }
    };

    public PlatformThrottleSettings GetPlatformSettings(VideoPlatform platform)
    {
        return PlatformSettings.TryGetValue(platform, out var settings) 
            ? settings 
            : PlatformSettings[VideoPlatform.Unknown];
    }
}
