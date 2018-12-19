using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using OtpNet;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity
{
    public class AuthenticatorTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IServiceProvider _serviceProvider;

        public AuthenticatorTokenProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
            if(string.IsNullOrWhiteSpace((string)provider?.MetaData["Key"]))
            {
                return false;
            }
            return await _serviceProvider.GetRequiredService<IUserService>()
                .TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.Authenticator, user);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
            var otp = new Totp(Base32Encoding.ToBytes((string)provider.MetaData["Key"]));

            long timeStepMatched;
            var valid = otp.VerifyTotp(token, out timeStepMatched, new VerificationWindow(1, 1));

            return Task.FromResult(valid);
        }
    }
}
