using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity;

public class EmailTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private const string CacheKeyFormat = "Email_TOTP_{0}_{1}";

    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly DistributedCacheEntryOptions _distributedCacheEntryOptions;

    public EmailTokenProvider(
        IServiceProvider serviceProvider,
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache)
    {
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
        _distributedCacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20)
        };
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(provider))
        {
            return false;
        }

        return await _serviceProvider.GetRequiredService<IUserService>().
            TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Email, user);
    }

    public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(provider))
        {
            return null;
        }

        return Task.FromResult(RedactEmail((string)provider.MetaData["Email"]));
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var cacheKey = string.Format(CacheKeyFormat, user.Id, token);
        var cachedValue = await _distributedCache.GetAsync(cacheKey);
        if (cachedValue != null)
        {
            return false;
        }

        var valid = await _serviceProvider.GetRequiredService<IUserService>().VerifyTwoFactorEmailAsync(user, token);
        if (valid)
        {
            await _distributedCache.SetAsync(cacheKey, [1], _distributedCacheEntryOptions);
        }

        return valid;
    }

    private bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData != null && provider.MetaData.ContainsKey("Email") &&
            !string.IsNullOrWhiteSpace((string)provider.MetaData["Email"]);
    }

    private static string RedactEmail(string email)
    {
        var emailParts = email.Split('@');

        string shownPart = null;
        if (emailParts[0].Length > 2 && emailParts[0].Length <= 4)
        {
            shownPart = emailParts[0].Substring(0, 1);
        }
        else if (emailParts[0].Length > 4)
        {
            shownPart = emailParts[0].Substring(0, 2);
        }
        else
        {
            shownPart = string.Empty;
        }

        string redactedPart = null;
        if (emailParts[0].Length > 4)
        {
            redactedPart = new string('*', emailParts[0].Length - 2);
        }
        else
        {
            redactedPart = new string('*', emailParts[0].Length - shownPart.Length);
        }

        return $"{shownPart}{redactedPart}@{emailParts[1]}";
    }
}
