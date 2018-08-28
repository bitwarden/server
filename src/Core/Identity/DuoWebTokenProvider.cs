using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Utilities.Duo;
using Bit.Core.Models;
using Bit.Core.Services;

namespace Bit.Core.Identity
{
    public class DuoWebTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IUserService _userService;
        private readonly GlobalSettings _globalSettings;

        public DuoWebTokenProvider(
            IUserService userService,
            GlobalSettings globalSettings)
        {
            _userService = userService;
            _globalSettings = globalSettings;
        }

        public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            if(!(await _userService.CanAccessPremium(user)))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return false;
            }

            return await user.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Duo, _userService);
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
