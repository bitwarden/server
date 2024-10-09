using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity;

public interface IOrganizationDuoUniversalTokenProvider : IOrganizationTwoFactorTokenProvider { }

public class OrganizationDuoUniversalTokenProvider : DuoTokenProvider, IOrganizationDuoUniversalTokenProvider
{
    private readonly IServiceProvider _serviceProvider;
    public OrganizationDuoUniversalTokenProvider(
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        ICurrentContext currentContext
        ) : base(currentContext, globalSettings)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return Task.FromResult(false);
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        var canGenerate = organization.TwoFactorProviderIsEnabled(TwoFactorProviderType.OrganizationDuo)
            && DuoUtilities.HasProperDuoMetadata(provider);
        return Task.FromResult(canGenerate);
    }

    public async Task<string> GenerateAsync(Organization organization, User user)
    {
        var provider = GetTwoFactorProvider(organization);
        if (provider == null)
        {
            return null;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return await GenerateAuthUrlAsync(provider, tokenDataFactory, user);
    }

    public async Task<bool> ValidateAsync(string token, Organization organization, User user)
    {
        var provider = GetTwoFactorProvider(organization);
        if (provider == null)
        {
            return false;
        }

        var tokenDataFactory = _serviceProvider.GetRequiredService<IDataProtectorTokenFactory<DuoUserStateTokenable>>();
        return await RequestDuoValidationAsync(provider, tokenDataFactory, user, token);
    }

    private TwoFactorProvider GetTwoFactorProvider(Organization organization)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return null;
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        if (!DuoUtilities.HasProperDuoMetadata(provider))
        {
            return null;
        }

        return provider;
    }
}
