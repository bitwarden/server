using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity;

public class DuoUniversalTokenProvider : DuoTokenProvider, IUserTwoFactorTokenProvider<User>
{
    private readonly IServiceProvider _serviceProvider;

    public DuoUniversalTokenProvider(
        IServiceProvider serviceProvider,
        GlobalSettings globalSettings,
        ICurrentContext currentContext)
        : base(currentContext, globalSettings)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var provider = await GetTwoFactorProvideAsync(user);
        if (provider == null)
        {
            return false;
        }

        var userService = _serviceProvider.GetRequiredService<IUserService>();
        return await userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Duo, user);
    }

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var provider = await GetTwoFactorProvideAsync(user);
        if (provider == null)
        {
            return null;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return await GenerateAuthUrlAsync(provider, tokenDataFactory, user);
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var provider = await GetTwoFactorProvideAsync(user);
        if (provider == null)
        {
            return false;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return await RequestDuoValidationAsync(provider, tokenDataFactory, user, token);
    }

    private async Task<TwoFactorProvider> GetTwoFactorProvideAsync(User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        if (!await userService.CanAccessPremium(user))
        {
            return null;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        if (!DuoUtilities.HasProperDuoMetadata(provider))
        {
            return null;
        }

        return provider;
    }
}
