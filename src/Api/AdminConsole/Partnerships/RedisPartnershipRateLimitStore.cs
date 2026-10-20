using StackExchange.Redis;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Keeps partnership request counters in the Redis instance used for distributed IP rate limiting.
/// </summary>
public sealed class RedisPartnershipRateLimitStore(IConnectionMultiplexer connectionMultiplexer)
    : IPartnershipRateLimitStore
{
    // A script so the increment and the expiry are atomic and a counter can't be left without an expiry.
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    public async Task<long> IncrementAsync(string key, TimeSpan expiry)
    {
        // Fail fast rather than queue the command until it times out.
        if (!connectionMultiplexer.IsConnected)
        {
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is not connected.");
        }

        var result = await connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(IncrementScript,
            [key], [(long)expiry.TotalMilliseconds]);
        return (long)result;
    }
}
