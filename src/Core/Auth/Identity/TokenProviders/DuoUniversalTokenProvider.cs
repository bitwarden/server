// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class DuoUniversalTokenProvider(
    IServiceProvider serviceProvider,
    IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
    IDuoUniversalTokenService duoUniversalTokenService) : IUserTwoFactorTokenProvider<User>
{
    /// <summary>
    /// We need the IServiceProvider to resolve the <see cref="IUserService"/>. There is a complex dependency dance
    /// occurring between <see cref="IUserService"/>, which extends the <see cref="UserManager{User}"/>, and the usage
    /// of the <see cref="UserManager{User}"/> within this class. Trying to resolve the <see cref="IUserService"/> using
    /// the DI pipeline will not allow the server to start and it will hang and give no helpful indication as to the
    /// problem.
    /// </summary>
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IDataProtectorTokenFactory<DuoUserStateTokenable> _tokenDataFactory = tokenDataFactory;
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = duoUniversalTokenService;

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var duoUniversalTokenProvider = await GetDuoTwoFactorProvider(user, userService);
        if (duoUniversalTokenProvider == null)
        {
            return false;
        }

        return duoUniversalTokenProvider.Enabled;
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
    /// Get the Duo Two Factor Provider for the user if they have premium access to Duo
    /// </summary>
    /// <param name="user">Active User</param>
    /// <returns>null or Duo TwoFactorProvider</returns>
    private async Task<TwoFactorProvider> GetDuoTwoFactorProvider(User user, IUserService userService)
    {
        if (!await userService.CanAccessPremium(user))
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
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var provider = await GetDuoTwoFactorProvider(user, userService);
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
