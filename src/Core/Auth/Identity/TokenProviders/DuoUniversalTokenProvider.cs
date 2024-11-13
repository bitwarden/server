using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Identity;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class DuoUniversalTokenProvider(
    IUserService userService,
    IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
    IDuoUniversalTokenService duoUniversalTokenService) : IUserTwoFactorTokenProvider<User>
{
    private readonly IUserService _userService = userService;
    private readonly IDataProtectorTokenFactory<DuoUserStateTokenable> _tokenDataFactory = tokenDataFactory;
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = duoUniversalTokenService;

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var provider = await GetDuoTwoFactorProvider(user);
        if (provider == null)
        {
            return false;
        }
        return await _userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Duo, user);
    }

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var duoClient = await GetDuoClientAsync(user);
        if (duoClient == null)
        {
            return null;
        }
        return _duoUniversalTokenService.GenerateAuthUrl(duoClient, _tokenDataFactory, user);
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var duoClient = await GetDuoClientAsync(user);
        if (duoClient == null)
        {
            return false;
        }
        return await _duoUniversalTokenService.RequestDuoValidationAsync(duoClient, _tokenDataFactory, user, token);
    }

    /// <summary>
    /// Get the Duo Two Factor Provider for the user if they have access to Duo
    /// </summary>
    /// <param name="user">Active User</param>
    /// <returns>null or Duo TwoFactorProvider</returns>
    private async Task<TwoFactorProvider> GetDuoTwoFactorProvider(User user)
    {
        if (!await _userService.CanAccessPremium(user))
        {
            return null;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        if (!_duoUniversalTokenService.HasProperDuoMetadata(provider))
        {
            return null;
        }

        return provider;
    }

    /// <summary>
    /// Uses the User to fetch a valid TwoFactorProvider and use it to create a Duo.Client
    /// </summary>
    /// <param name="user">active user</param>
    /// <returns>null or Duo TwoFactorProvider</returns>
    private async Task<Duo.Client> GetDuoClientAsync(User user)
    {
        var provider = await GetDuoTwoFactorProvider(user);
        if (provider == null)
        {
            return null;
        }

        var duoClient = await _duoUniversalTokenService.BuildDuoTwoFactorClientAsync(provider);
        if (duoClient == null)
        {
            return null;
        }

        return duoClient;
    }
}
