namespace Bit.Core.Settings;

public class DistributedIpRateLimitingSettings
{
    public string RedisConnectionString { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of Redis timeouts that can be experienced within the sliding timeout
    /// window before IP rate limiting is temporarily disabled.
    /// TODO: Determine/discuss a suitable maximum
    /// </summary>
    public int MaxRedisTimeoutsThreshold { get; set; } = 10;

    /// <summary>
    /// Length of the sliding window in seconds to track Redis timeout exceptions.
    /// TODO: Determine/discuss a suitable sliding window
    /// </summary>
    public int SlidingWindowSeconds { get; set; } = 120;
}
