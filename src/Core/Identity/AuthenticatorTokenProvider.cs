using System;
using System.Threading.Tasks;
using Base32;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Domains;
using Bit.Core.Enums;
using OtpSharp;

namespace Bit.Core.Identity
{
    public class AuthenticatorTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var canGenerate = user.TwoFactorEnabled
                && user.TwoFactorProvider.HasValue
                && user.TwoFactorProvider.Value == TwoFactorProvider.Authenticator
                && !string.IsNullOrWhiteSpace(user.AuthenticatorKey);

            return Task.FromResult(canGenerate);
        }

        public Task<string> GetUserModifierAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var otp = new Totp(Base32Encoder.Decode(user.AuthenticatorKey));

            long timeStepMatched;
            var valid = otp.VerifyTotp(token, out timeStepMatched, new VerificationWindow(2, 2));

            return Task.FromResult(valid);
        }
    }
}
