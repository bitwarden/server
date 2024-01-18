using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Utilities.Duo;
using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Core.Auth.Identity;

public interface IOrganizationDuoWebTokenProvider : IOrganizationTwoFactorTokenProvider { }

public class OrganizationDuoWebTokenProvider : IOrganizationDuoWebTokenProvider
{
    private readonly GlobalSettings _globalSettings;

    public OrganizationDuoWebTokenProvider(
        GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return Task.FromResult(false);
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        var canGenerate = organization.TwoFactorProviderIsEnabled(TwoFactorProviderType.OrganizationDuo)
            && HasProperMetaData(provider);
        return Task.FromResult(canGenerate);
    }

    public async Task<string> GenerateAsync(Organization organization, User user)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return null;
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        if (!HasProperMetaData(provider))
        {
            return null;
        }

        var signatureRequest = DuoWeb.SignRequest((string)provider.MetaData["IKey"],
            (string)provider.MetaData["SKey"], _globalSettings.Duo.AKey, user.Email);

        return signatureRequest;
    }

    public async Task<bool> ValidateAsync(string token, Organization organization, User user)
    {
        if (organization == null || !organization.Enabled || !organization.Use2fa)
        {
            return false;
        }

        var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
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
