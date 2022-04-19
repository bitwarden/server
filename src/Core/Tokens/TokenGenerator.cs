using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Tokens
{
    /// <summary>
    /// Generates unique encrypted tokens for SSO
    /// </summary>
    public class TokenGenerator
    {
        private const string TokenIssuer     = "BitwardenSSO-a9d675e1-bbde-4015-a46e-07522903fd86";
        private const string SigningKey      = "0BE055E5886A4DFF3244F19CAAC9FA5CD42EDA39197A61BA6315EDD69933BE59";
        private const string Audience        = "A57A88D5AAB57980A3D874D06D0E693A41BBBF4C2A25683CC972895057E902B0";
        private const int TokenExpirySeconds = 30;

        /// <summary>
        /// Generates a valid expiring token
        /// </summary>
        /// <returns></returns>
        public string GenerateToken()
        {
            var generator = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var date = DateTime.UtcNow;

            var token = new JwtSecurityToken(
                TokenIssuer,
                Audience,
                expires: date.AddSeconds(TokenExpirySeconds),
                signingCredentials: creds);
            var tokenString = generator.WriteToken(token);

            return tokenString;
        }

        /// <summary>
        /// Validates the passed in token ensuring timing and issuer
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool ValidateToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var securityToken = default(SecurityToken);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            var validationParams = new TokenValidationParameters();
            validationParams.IssuerSigningKey = key;
            validationParams.ValidAudiences = new string[] { Audience };
            validationParams.ValidIssuer = TokenIssuer;

            var principal = handler.ValidateToken(token, validationParams, out securityToken);
            var expClaim = principal.Claims.First().Subject.Claims.First();

            return securityToken.Issuer == TokenIssuer;
        }
    }
}
