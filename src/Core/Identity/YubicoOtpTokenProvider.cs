using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using YubicoDotNetClient;
using System.Linq;

namespace Bit.Core.Identity
{
    public class YubicoOtpTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly GlobalSettings _globalSettings;

        public YubicoOtpTokenProvider(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            if(!user.Premium)
            {
                return Task.FromResult(false);
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.YubiKey)
                && (provider?.MetaData.Values.Any(v => !string.IsNullOrWhiteSpace((string)v)) ?? false);

            return Task.FromResult(canGenerate);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            if(!user.Premium)
            {
                return Task.FromResult(false);
            }

            if(string.IsNullOrWhiteSpace(token) || token.Length != 44)
            {
                return Task.FromResult(false);
            }

            var id = token.Substring(0, 12);

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
            if(!provider.MetaData.ContainsValue(id))
            {
                return Task.FromResult(false);
            }

            var client = new YubicoClient(_globalSettings.Yubico.ClientId, _globalSettings.Yubico.Key);
            var response = client.Verify(token);
            return Task.FromResult(response.Status == YubicoResponseStatus.Ok);
        }
    }
}
