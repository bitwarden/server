using System.Globalization;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Sso;

public class Saml2Rsa15DeprecationNoticeInterval : ISaml2Rsa15DeprecationNoticeInterval
{
    private readonly IDistributedCache _cache;
    private readonly IGlobalSettings _globalSettings;
    private readonly TimeProvider _timeProvider;

    public Saml2Rsa15DeprecationNoticeInterval(
        // "persistent" is a well-known keyed service registered by AddDistributedCache(globalSettings).
        [FromKeyedServices("persistent")] IDistributedCache cache,
        IGlobalSettings globalSettings,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _globalSettings = globalSettings;
        _timeProvider = timeProvider;
    }

    public async Task<bool> TryClaimIntervalAsync(Guid organizationId)
    {
        var days = _globalSettings.Sso.Rsa15DeprecationEmailIntervalInDays;
        if (days <= 0)
        {
            return false;
        }

        var interval = TimeSpan.FromDays(days);
        var key = CacheKey(organizationId);
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;

        var stored = await _cache.GetStringAsync(key);
        if (long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sentTicks)
            && nowTicks - sentTicks < interval.Ticks)
        {
            return false;
        }

        await _cache.SetStringAsync(
            key,
            nowTicks.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = interval });
        return true;
    }

    private static string CacheKey(Guid organizationId) => $"sso:saml2:rsa15-deprecation-email:{organizationId}";
}
