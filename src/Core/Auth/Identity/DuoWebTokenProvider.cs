using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities.Duo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity;

public class DuoWebTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GlobalSettings _globalSettings;

    public DuoWebTokenProvider(
        IServiceProvider serviceProvider,
        GlobalSettings globalSettings)
    {
        _serviceProvider = serviceProvider;
        _globalSettings = globalSettings;
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        if (!(await userService.CanAccessPremium(user)))
        {
            return false;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        if (!HasProperMetaData(provider))
        {
            return false;
        }

        return await userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Duo, user);
    }

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        if (!(await userService.CanAccessPremium(user)))
        {
            return null;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        if (!HasProperMetaData(provider))
        {
            return null;
        }

        var signatureRequest = DuoWeb.SignRequest((string)provider.MetaData["IKey"],
            (string)provider.MetaData["SKey"], _globalSettings.Duo.AKey, user.Email);
        return signatureRequest;
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        if (!(await userService.CanAccessPremium(user)))
        {
            return false;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        if (!HasProperMetaData(provider))
        {
            return false;
        }

        var response = DuoWeb.VerifyResponse((string)provider.MetaData["IKey"], (string)provider.MetaData["SKey"],
            _globalSettings.Duo.AKey, token);

        return response == user.Email;
    }

    private bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData != null && provider.MetaData.ContainsKey("IKey") &&
            provider.MetaData.ContainsKey("SKey") && provider.MetaData.ContainsKey("Host");
    }
}
