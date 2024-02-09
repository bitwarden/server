using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity;

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

    public static string RedactEmail(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (!HasProperMetaData(provider))
        {
            return null;
        }

        var email = (string)provider.MetaData["Email"];
        var emailParts = email.Split('@');

        string shownPart;
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

        string redactedPart;
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

    private static bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData != null && provider.MetaData.ContainsKey("Email") &&
            !string.IsNullOrWhiteSpace((string)provider.MetaData["Email"]);
    }
}
