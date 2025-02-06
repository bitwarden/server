using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity.TokenProviders;

public interface IOrganizationDuoUniversalTokenProvider : IOrganizationTwoFactorTokenProvider { }

public class OrganizationDuoUniversalTokenProvider(
    IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
    IDuoUniversalTokenService duoUniversalTokenService) : IOrganizationDuoUniversalTokenProvider
{
    private readonly IDataProtectorTokenFactory<DuoUserStateTokenable> _tokenDataFactory = tokenDataFactory;
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = duoUniversalTokenService;

    public Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization)
    {
        var provider = GetDuoTwoFactorProvider(organization);
        if (provider != null && provider.Enabled)
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<string> GenerateAsync(Organization organization, User user)
    {
        var duoClient = await GetDuoClientAsync(organization);
        if (duoClient == null)
        {
            return null;
        }
        return _duoUniversalTokenService.GenerateAuthUrl(duoClient, _tokenDataFactory, user);
    }

    public async Task<bool> ValidateAsync(string token, Organization organization, User user)
    {
        var duoClient = await GetDuoClientAsync(organization);
        if (duoClient == null)
        {
            return false;
        }
        return await _duoUniversalTokenService.RequestDuoValidationAsync(duoClient, _tokenDataFactory, user, token);
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
