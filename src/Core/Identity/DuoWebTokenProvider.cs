using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Utilities.Duo;
using Bit.Core.Models;

namespace Bit.Core.Identity
{
    public class DuoWebTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly GlobalSettings _globalSettings;

        public DuoWebTokenProvider(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            if(!user.Premium)
            {
                return Task.FromResult(false);
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.Duo) && HasProperMetaData(provider);
            return Task.FromResult(canGenerate);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            if(!user.Premium)
            {
                return Task.FromResult<string>(null);
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return Task.FromResult<string>(null);
            }

            var signatureRequest = DuoWeb.SignRequest((string)provider.MetaData["IKey"], (string)provider.MetaData["SKey"],
                _globalSettings.Duo.AKey, user.Email);
            return Task.FromResult(signatureRequest);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            if(!user.Premium)
            {
                return Task.FromResult(false);
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return Task.FromResult(false);
            }

            var response = DuoWeb.VerifyResponse((string)provider.MetaData["IKey"], (string)provider.MetaData["SKey"],
                _globalSettings.Duo.AKey, token);

            return Task.FromResult(response == user.Email);
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return provider?.MetaData != null && provider.MetaData.ContainsKey("IKey") &&
                provider.MetaData.ContainsKey("SKey") && provider.MetaData.ContainsKey("Host");
        }
    }
}
