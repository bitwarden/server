using System;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Identity
{
    public class JwtBearerIdentityOptions
    {
        public string Audience { get; set; }
        public string Issuer { get; set; }
        public SigningCredentials SigningCredentials { get; set; }
        public TimeSpan? TokenLifetime { get; set; }
        public TimeSpan? TwoFactorTokenLifetime { get; set; }
        public string AuthenticationMethod { get; set; } = "Application";
        public string TwoFactorAuthenticationMethod { get; set; } = "TwoFactor";
    }
}
