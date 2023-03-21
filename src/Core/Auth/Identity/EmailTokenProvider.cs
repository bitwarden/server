using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity;

public class EmailTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private readonly IServiceProvider _serviceProvider;

    public EmailTokenProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        return _serviceProvider.GetRequiredService<IUserService>().VerifyTwoFactorEmailAsync(user, token);
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
