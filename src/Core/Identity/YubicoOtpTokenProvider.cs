using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using YubicoDotNetClient;
using System.Linq;
using Bit.Core.Services;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity
{
    public class YubicoOtpTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly GlobalSettings _globalSettings;

        public YubicoOtpTokenProvider(
            IServiceProvider serviceProvider,
            GlobalSettings globalSettings)
        {
            _serviceProvider = serviceProvider;
            _globalSettings = globalSettings;
        }

        public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
            if(!provider?.MetaData.Values.Any(v => !string.IsNullOrWhiteSpace((string)v)) ?? true)
            {
                return false;
            }

            return await userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.YubiKey, user);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)))
            {
                return false;
            }

            if(string.IsNullOrWhiteSpace(token) || token.Length != 44)
            {
                return false;
            }

            var id = token.Substring(0, 12);

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
            if(!provider.MetaData.ContainsValue(id))
            {
                return false;
            }

            var client = new YubicoClient(_globalSettings.Yubico.ClientId, _globalSettings.Yubico.Key);
            var response = await client.VerifyAsync(token);
            return response.Status == YubicoResponseStatus.Ok;
        }
    }
}
