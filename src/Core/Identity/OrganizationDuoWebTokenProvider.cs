using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Utilities.Duo;
using Bit.Core.Models;
using Bit.Core.Settings;

namespace Bit.Core.Identity
{
    public interface IOrganizationDuoWebTokenProvider : IOrganizationTwoFactorTokenProvider { }

    public class OrganizationDuoWebTokenProvider : IOrganizationDuoWebTokenProvider
    {
        private readonly GlobalSettings _globalSettings;

        public OrganizationDuoWebTokenProvider(GlobalSettings globalSettings)
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

        public Task<string> GenerateAsync(Organization organization, User user)
        {
            if (organization == null || !organization.Enabled || !organization.Use2fa)
            {
                return Task.FromResult<string>(null);
            }

            var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
            if (!HasProperMetaData(provider))
            {
                return Task.FromResult<string>(null);
            }

            var signatureRequest = DuoWeb.SignRequest((string)provider.MetaData["IKey"],
                (string)provider.MetaData["SKey"], _globalSettings.Duo.AKey, user.Email);
            return Task.FromResult(signatureRequest);
        }

        public Task<bool> ValidateAsync(string token, Organization organization, User user)
        {
            if (organization == null || !organization.Enabled || !organization.Use2fa)
            {
                return Task.FromResult(false);
            }

            var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
            if (!HasProperMetaData(provider))
            {
                return Task.FromResult(false);
            }

            var response = DuoWeb.VerifyResponse((string)provider.MetaData["IKey"],
                (string)provider.MetaData["SKey"], _globalSettings.Duo.AKey, token);

            return Task.FromResult(response == user.Email);
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return provider?.MetaData != null && provider.MetaData.ContainsKey("IKey") &&
                provider.MetaData.ContainsKey("SKey") && provider.MetaData.ContainsKey("Host");
        }
    }
}
