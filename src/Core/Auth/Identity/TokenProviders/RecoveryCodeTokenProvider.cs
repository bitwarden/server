using System.Text;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class RecoveryCodeTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private const string CacheKeyFormat = "RecoveryCodeToken_{0}_{1}_{2}";

    private readonly IDistributedCache _distributedCache;
    private readonly DistributedCacheEntryOptions _distributedCacheEntryOptions;

    public RecoveryCodeTokenProvider(
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
        _distributedCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
    }

    public virtual Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.TwoFactorRecoveryCode));
    }

    public virtual async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        if (user.TwoFactorRecoveryCode == null)
        {
            throw new ArgumentNullException(nameof(user.TwoFactorRecoveryCode));
        }

        var code = user.TwoFactorRecoveryCode;
        var cacheKey = string.Format(CacheKeyFormat, user.Id, user.SecurityStamp, purpose);
        await _distributedCache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(code), _distributedCacheEntryOptions);
        return code;
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        // var cacheKey = string.Format(CacheKeyFormat, user.Id, user.SecurityStamp, purpose);
        // var cachedValue = await _distributedCache.GetAsync(cacheKey);
        // if (cachedValue == null)
        // {
        //     return false;
        // }
        //
        // var code = Encoding.UTF8.GetString(cachedValue);
        // var valid = string.Equals(token, code);
        // if (valid)
        // {
        //     await _distributedCache.RemoveAsync(cacheKey);
        // }
        //
        // return valid;

        var processedToken = token.Replace(" ", string.Empty).ToLower();
        return string.Equals(processedToken, user.TwoFactorRecoveryCode);
    }
}
