// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// Generates tokens for email two-factor authentication.
/// It inherits from the EmailTokenProvider class, which manages the persistence and validation of tokens, 
/// and adds additional validation to ensure that 2FA is enabled for the user.
/// </summary>
public class EmailTwoFactorTokenProvider : EmailTokenProvider
{
    public EmailTwoFactorTokenProvider(
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache,
        IFeatureService featureService) :
        base(distributedCache, featureService)
    {
        // This can be removed when the pm-18612-otp-6-digits feature flag is removed because the base implementation will match.
        TokenAlpha = false;
        TokenNumeric = true;
        TokenLength = 6;
    }

    public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var emailTokenProvider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(emailTokenProvider))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(emailTokenProvider.Enabled);
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
        return provider?.MetaData != null && provider.MetaData.TryGetValue("Email", out var emailValue) &&
            !string.IsNullOrWhiteSpace((string)emailValue);
    }
}
