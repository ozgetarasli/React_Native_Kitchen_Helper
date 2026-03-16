namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Service for rotating User-Agent strings to avoid detection.
/// Provides a pool of realistic browser User-Agent strings.
/// </summary>
public interface IUserAgentRotator
{
    /// <summary>
    /// Gets a random User-Agent string from the pool.
    /// </summary>
    string GetRandomUserAgent();
    
    /// <summary>
    /// Gets a User-Agent string suitable for the specified platform.
    /// </summary>
    string GetUserAgentForPlatform(Kitchenhelper.Core.Services.VideoPlatform platform);
}

/// <summary>
/// Implementation of User-Agent rotation service.
/// </summary>
public class UserAgentRotator : IUserAgentRotator
{
    private readonly Random _random = new();
    
    // Realistic browser User-Agent strings (updated for 2025-2026)
    private static readonly string[] DesktopUserAgents = new[]
    {
        // Chrome Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 11.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        
        // Firefox Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:122.0) Gecko/20100101 Firefox/122.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:123.0) Gecko/20100101 Firefox/123.0",
        
        // Edge Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36 Edg/121.0.0.0",
        
        // Chrome macOS
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
        
        // Safari macOS
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.3 Safari/605.1.15",
    };

    // Mobile User-Agents for platforms that prefer mobile (TikTok, Instagram)
    private static readonly string[] MobileUserAgents = new[]
    {
        // iPhone Safari
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.3 Mobile/15E148 Safari/604.1",
        
        // iPhone Chrome
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/120.0.6099.119 Mobile/15E148 Safari/604.1",
        
        // Android Chrome
        "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.144 Mobile Safari/537.36",
        "Mozilla/5.0 (Linux; Android 14; Pixel 8 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.144 Mobile Safari/537.36",
        "Mozilla/5.0 (Linux; Android 13; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.144 Mobile Safari/537.36",
    };

    // Platforms that work better with mobile User-Agents
    private static readonly HashSet<Kitchenhelper.Core.Services.VideoPlatform> MobilePreferredPlatforms = new()
    {
        Kitchenhelper.Core.Services.VideoPlatform.TikTok,
        Kitchenhelper.Core.Services.VideoPlatform.Instagram
    };

    public string GetRandomUserAgent()
    {
        // 70% desktop, 30% mobile
        var useDesktop = _random.NextDouble() < 0.7;
        var pool = useDesktop ? DesktopUserAgents : MobileUserAgents;
        return pool[_random.Next(pool.Length)];
    }

    public string GetUserAgentForPlatform(Kitchenhelper.Core.Services.VideoPlatform platform)
    {
        // Mobile platforms prefer mobile User-Agents
        if (MobilePreferredPlatforms.Contains(platform))
        {
            return MobileUserAgents[_random.Next(MobileUserAgents.Length)];
        }
        
        return DesktopUserAgents[_random.Next(DesktopUserAgents.Length)];
    }
}
