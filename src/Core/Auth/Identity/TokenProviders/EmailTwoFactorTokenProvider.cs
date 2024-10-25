using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class EmailTwoFactorTokenProvider : EmailTokenProvider
{
    private readonly IServiceProvider _serviceProvider;

    public EmailTwoFactorTokenProvider(
        IServiceProvider serviceProvider,
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache) :
        base(distributedCache)
    {
        _serviceProvider = serviceProvider;

        TokenAlpha = false;
        TokenNumeric = true;
        TokenLength = 6;
    }

    public override async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(provider))
        {
            return false;
        }

        return await _serviceProvider.GetRequiredService<IUserService>().
            TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Email, user);
    }

    public override Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(provider))
        {
            return null;
        }

        return base.GenerateAsync(purpose, manager, user);
    }

    private static bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData != null && provider.MetaData.ContainsKey("Email") &&
            !string.IsNullOrWhiteSpace((string)provider.MetaData["Email"]);
    }
}
