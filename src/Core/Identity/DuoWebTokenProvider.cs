using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Utilities.Duo;
using System;
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
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.Duo) && HasProperMetaData(provider);
            return Task.FromResult(canGenerate);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return Task.FromResult<string>(null);
            }

            var signatureRequest = DuoWeb.SignRequest(provider.MetaData["IKey"], provider.MetaData["SKey"],
                _globalSettings.Duo.AKey, user.Id.ToString());
            return Task.FromResult(signatureRequest);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return Task.FromResult(false);
            }

            var response = DuoWeb.VerifyResponse(provider.MetaData["IKey"], provider.MetaData["SKey"],
                _globalSettings.Duo.AKey, token);

            Guid userId;
            if(!Guid.TryParse(response, out userId))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(userId == user.Id);
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return provider?.MetaData != null && provider.MetaData.ContainsKey("IKey") &&
                provider.MetaData.ContainsKey("SKey") && provider.MetaData.ContainsKey("Host");
        }
    }
}
