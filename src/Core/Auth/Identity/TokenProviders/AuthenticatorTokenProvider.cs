// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class AuthenticatorTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private const string CacheKeyFormat = "Authenticator_TOTP_{0}_{1}";

    private readonly IDistributedCache _distributedCache;
    private readonly DistributedCacheEntryOptions _distributedCacheEntryOptions;

    public AuthenticatorTokenProvider(
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
        _distributedCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        };
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var authenticatorProvider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
        if (string.IsNullOrWhiteSpace((string)authenticatorProvider?.MetaData["Key"]))
        {
            return Task.FromResult(false);
        }
        return Task.FromResult(authenticatorProvider.Enabled);
    }

    public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        return Task.FromResult<string>(null);
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var cacheKey = string.Format(CacheKeyFormat, user.Id, token);
        var cachedValue = await _distributedCache.GetAsync(cacheKey);
        if (cachedValue != null)
        {
            return false;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
        var otp = new Totp(Base32Encoding.ToBytes((string)provider.MetaData["Key"]));

        // TODO: the out var is a timestepMatched; consider logging it.
        var valid = otp.VerifyTotp(token, out _, new VerificationWindow(1, 1));

        if (valid)
        {
            await _distributedCache.SetAsync(cacheKey, [1], _distributedCacheEntryOptions);
        }

        return valid;
    }
}
