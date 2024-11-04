using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity.TokenProviders;

public interface IOrganizationDuoUniversalTokenProvider : IOrganizationTwoFactorTokenProvider { }

public class OrganizationDuoUniversalTokenProvider(
    IServiceProvider serviceProvider,
    IDuoUniversalTokenService duoUniversalTokenService) : IOrganizationDuoUniversalTokenProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = duoUniversalTokenService;

    public Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return Task.FromResult(false);
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        var canGenerate = organization.TwoFactorProviderIsEnabled(TwoFactorProviderType.OrganizationDuo)
            && _duoUniversalTokenService.HasProperDuoMetadata(provider);
        return Task.FromResult(canGenerate);
    }

    public async Task<string> GenerateAsync(Organization organization, User user)
    {
        var duoClient = await GetDuoClientAsync(organization);
        if (duoClient == null)
        {
            return null;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return _duoUniversalTokenService.GenerateAuthUrl(duoClient, tokenDataFactory, user);
    }

    public async Task<bool> ValidateAsync(string token, Organization organization, User user)
    {
        var duoClient = await GetDuoClientAsync(organization);
        if (duoClient == null)
        {
            return false;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return await _duoUniversalTokenService.RequestDuoValidationAsync(duoClient, tokenDataFactory, user, token);
    }

    private TwoFactorProvider GetDuoTwoFactorProvider(Organization organization)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return null;
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        if (!_duoUniversalTokenService.HasProperDuoMetadata(provider))
        {
            return null;
        }
        return provider;
    }

    private async Task<Duo.Client> GetDuoClientAsync(Organization organization)
    {
        var provider = GetDuoTwoFactorProvider(organization);
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
