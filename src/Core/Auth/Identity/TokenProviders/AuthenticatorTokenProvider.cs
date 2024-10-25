using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class AuthenticatorTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private const string CacheKeyFormat = "Authenticator_TOTP_{0}_{1}";

    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly DistributedCacheEntryOptions _distributedCacheEntryOptions;

    public AuthenticatorTokenProvider(
        IServiceProvider serviceProvider,
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache)
    {
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
        _distributedCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        };
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
        if (string.IsNullOrWhiteSpace((string)provider?.MetaData["Key"]))
        {
            return false;
        }
        return await _serviceProvider.GetRequiredService<IUserService>()
            .TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Authenticator, user);
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
        var valid = otp.VerifyTotp(token, out _, new VerificationWindow(1, 1));

        if (valid)
        {
            await _distributedCache.SetAsync(cacheKey, [1], _distributedCacheEntryOptions);
        }

        return valid;
    }
}
