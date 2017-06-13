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
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);

            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.YubiKey)
                && user.TwoFactorProvider.HasValue
                && user.TwoFactorProvider.Value == TwoFactorProviderType.YubiKey
                && (provider?.MetaData.Values.Any(v => !string.IsNullOrWhiteSpace(v)) ?? false);

            return Task.FromResult(canGenerate);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            if(token.Length != 44)
            {
                return Task.FromResult(false);
            }

            var id = token.Substring(0, 12);

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
            if(!provider.MetaData.ContainsValue(id))
            {
                return Task.FromResult(false);
            }

            var client = new YubicoClient("TODO", "TODO");
            var response = client.Verify(token);
            return Task.FromResult(response.Status == YubicoResponseStatus.Ok);
        }
    }
}
