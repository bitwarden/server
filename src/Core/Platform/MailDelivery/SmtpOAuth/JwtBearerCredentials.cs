#nullable enable

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Platform.MailDelivery;

internal class JwtBearerCredentials : OAuthHandler
{
    private readonly string _algorithm;
    private readonly string _signingKey;
    private readonly IEnumerable<KeyValuePair<string, string?>> _claims;

    public JwtBearerCredentials(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        string tokenEndpoint,
        string username,
        string algorithm,
        string signingKey,
        IEnumerable<KeyValuePair<string, string?>> claims)
        : base(httpClientFactory, timeProvider, tokenEndpoint, username)
    {
        _algorithm = algorithm;
        _signingKey = signingKey;
        _claims = claims;
    }

    protected override FormUrlEncodedContent BuildContent()
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var now = DateTime.UtcNow;

        SigningCredentials signingCredentials;
        IDisposable? disposable = null;

        try
        {
            if (string.Equals(_algorithm, SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                // Will this dispose to early?
                var rsa = RSA.Create();
                disposable = rsa;
                rsa.ImportFromPem(_signingKey);
                signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
            }
            else
            {
                throw new NotImplementedException("");
            }

            var claims = new List<Claim>();

            foreach (var claim in _claims)
            {
                if (string.IsNullOrEmpty(claim.Value))
                {
                    continue;
                }

                claims.Add(new Claim(claim.Key, claim.Value));
            }

            // For in iat claim it's not required via the spec but we know google does require it and the spec does
            // say that extra claims should be allowed, if we ever run into a provider that doesn't want this
            // we can add a settings turning it off and default google to on.
            claims.Add(new Claim(JwtClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString()));

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(5),
                signingCredentials
            );

            return new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", tokenHandler.WriteToken(token) },
            });
        }
        finally
        {
            disposable?.Dispose();
        }
    }
}
