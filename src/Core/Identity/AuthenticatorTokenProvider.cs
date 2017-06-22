using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using OtpNet;

namespace Bit.Core.Identity
{
    public class AuthenticatorTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);

            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.Authenticator)
                && !string.IsNullOrWhiteSpace((string)provider.MetaData["Key"]);
            
            return Task.FromResult(canGenerate);
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
