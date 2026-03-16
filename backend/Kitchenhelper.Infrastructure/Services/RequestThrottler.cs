using System.Collections.Concurrent;
using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Service for managing request throttling and rate limiting.
/// Prevents IP bans and respects platform rate limits.
/// </summary>
public interface IRequestThrottler
{
    /// <summary>
    /// Waits for the appropriate delay before making a request to a platform.
    /// Returns true if the request can proceed, false if rate limited.
    /// </summary>
    Task<bool> WaitForRequestAsync(VideoPlatform platform, string? userId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records a completed request for tracking purposes.
    /// </summary>
    void RecordRequest(VideoPlatform platform, string? userId = null, bool success = true);
    
    /// <summary>
    /// Checks if a user has exceeded their rate limit.
    /// </summary>
    bool IsUserRateLimited(string userId);
    
    /// <summary>
    /// Gets the number of requests remaining for a user in the current hour/day.
    /// </summary>
    (int hourlyRemaining, int dailyRemaining) GetUserQuota(string userId);
}

/// <summary>
/// Implementation of request throttling with per-platform delays and user rate limiting.
/// </summary>
public class RequestThrottler : IRequestThrottler
{
    private readonly ILogger<RequestThrottler> _logger;
    private readonly IVideoDownloadSettings _settings;
    private readonly Random _random = new();
    
    // Track last request time per platform
    private readonly ConcurrentDictionary<VideoPlatform, DateTime> _lastPlatformRequest = new();
    
    // Track user request counts (userId -> (hourlyCount, dailyCount, hourStart, dayStart))
    private readonly ConcurrentDictionary<string, UserRequestStats> _userStats = new();
    
    // Lock objects for thread safety
    private readonly ConcurrentDictionary<VideoPlatform, SemaphoreSlim> _platformLocks = new();

    public RequestThrottler(
        ILogger<RequestThrottler> logger,
        IVideoDownloadSettings settings)
    {
        _logger = logger;
        _settings = settings;
        
        // Initialize locks for each platform
        foreach (VideoPlatform platform in Enum.GetValues<VideoPlatform>())
        {
            _platformLocks[platform] = new SemaphoreSlim(1, 1);
        }
    }

    public async Task<bool> WaitForRequestAsync(VideoPlatform platform, string? userId = null, CancellationToken cancellationToken = default)
    {
        // Check user rate limit first
        if (!string.IsNullOrEmpty(userId) && IsUserRateLimited(userId))
        {
            _logger.LogWarning("User {UserId} has exceeded their rate limit", userId);
            return false;
        }

        var platformLock = _platformLocks.GetOrAdd(platform, _ => new SemaphoreSlim(1, 1));
        
        try
        {
            await platformLock.WaitAsync(cancellationToken);
            
            var platformSettings = _settings.GetPlatformSettings(platform);
            
            // Calculate required delay
            if (_lastPlatformRequest.TryGetValue(platform, out var lastRequest))
            {
                var elapsed = DateTime.UtcNow - lastRequest;
                var requiredDelay = GetRandomDelay(platformSettings.MinDelayMs, platformSettings.MaxDelayMs);
                var remainingDelay = requiredDelay - (int)elapsed.TotalMilliseconds;
                
                if (remainingDelay > 0)
                {
                    _logger.LogDebug(
                        "Throttling request to {Platform}: waiting {Delay}ms",
                        platform, remainingDelay);
                    
                    await Task.Delay(remainingDelay, cancellationToken);
                }
            }
            
            // Record the request time
            _lastPlatformRequest[platform] = DateTime.UtcNow;
            
            return true;
        }
        finally
        {
            platformLock.Release();
        }
    }

    public void RecordRequest(VideoPlatform platform, string? userId = null, bool success = true)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        var now = DateTime.UtcNow;
        
        _userStats.AddOrUpdate(
            userId,
            _ => new UserRequestStats
            {
                HourlyCount = 1,
                DailyCount = 1,
                HourStart = now,
                DayStart = now.Date
            },
            (_, stats) =>
            {
                // Reset hourly count if hour has passed
                if ((now - stats.HourStart).TotalHours >= 1)
                {
                    stats.HourlyCount = 0;
                    stats.HourStart = now;
                }
                
                // Reset daily count if day has passed
                if (now.Date > stats.DayStart)
                {
                    stats.DailyCount = 0;
                    stats.DayStart = now.Date;
                }
                
                stats.HourlyCount++;
                stats.DailyCount++;
                
                return stats;
            });
        
        _logger.LogDebug(
            "Recorded request for user {UserId}: {Platform}, success={Success}",
            userId, platform, success);
    }

    public bool IsUserRateLimited(string userId)
    {
        if (!_userStats.TryGetValue(userId, out var stats))
            return false;

        var now = DateTime.UtcNow;
        
        // Check if stats need reset
        var hourlyCount = (now - stats.HourStart).TotalHours >= 1 ? 0 : stats.HourlyCount;
        var dailyCount = now.Date > stats.DayStart ? 0 : stats.DailyCount;
        
        return hourlyCount >= _settings.MaxDownloadsPerHour || 
               dailyCount >= _settings.MaxDownloadsPerDay;
    }

    public (int hourlyRemaining, int dailyRemaining) GetUserQuota(string userId)
    {
        if (!_userStats.TryGetValue(userId, out var stats))
            return (_settings.MaxDownloadsPerHour, _settings.MaxDownloadsPerDay);

        var now = DateTime.UtcNow;
        
        var hourlyCount = (now - stats.HourStart).TotalHours >= 1 ? 0 : stats.HourlyCount;
        var dailyCount = now.Date > stats.DayStart ? 0 : stats.DailyCount;
        
        return (
            Math.Max(0, _settings.MaxDownloadsPerHour - hourlyCount),
            Math.Max(0, _settings.MaxDownloadsPerDay - dailyCount)
        );
    }

    private int GetRandomDelay(int min, int max)
    {
        return _random.Next(min, max + 1);
    }

    private class UserRequestStats
    {
        public int HourlyCount { get; set; }
        public int DailyCount { get; set; }
        public DateTime HourStart { get; set; }
        public DateTime DayStart { get; set; }
    }
}
