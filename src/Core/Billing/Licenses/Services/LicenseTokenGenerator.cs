using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;
using JwtSecurityTokenHandler = System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler;

namespace Bit.Core.Billing.Licenses.Services;

/// <summary>
/// Single source of the license-token contract (issuer, algorithm, lifetime). Both
/// <c>LicensingService</c> and the seeder mint tokens through here so the shape stays in sync.
/// </summary>
public static class LicenseTokenGenerator
{
    private const string _issuer = "bitwarden";

    public static string Generate(X509Certificate2 certificate, List<Claim> claims, string audience)
    {
        if (claims.All(claim => claim.Type != JwtClaimTypes.JwtId))
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()));
        }

        using var rsa = certificate.GetRSAPrivateKey();
        var securityKey = new RsaSecurityKey(rsa);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = audience,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddYears(1), // Org expiration is a claim
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
