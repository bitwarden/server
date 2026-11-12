using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Platform.Mail.Delivery;

// Handles the jwt-bearer grant for SMTP OAuth token exchange by signing an RS256 assertion.
// Carries no provider-specific knowledge: callers resolve the token endpoint, signing key, and
// claims before constructing this handler.
internal sealed class JwtBearerHandler : OAuthHandler
{
    public const string GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

    private const string SubjectClaimName = "sub";

    private static readonly TimeSpan _assertionLifetime = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly string _outboundMailbox;
    private readonly string _signingKey;
    private readonly IReadOnlyDictionary<string, string> _claims;

    public JwtBearerHandler(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger<JwtBearerHandler> logger,
        string tokenEndpoint,
        string outboundMailbox,
        string signingKey,
        IReadOnlyDictionary<string, string> claims)
        : base(httpClientFactory, timeProvider, logger, tokenEndpoint, outboundMailbox)
    {
        _timeProvider = timeProvider;
        _outboundMailbox = outboundMailbox;
        _signingKey = signingKey;
        _claims = claims;
    }

    protected override Dictionary<string, string> BuildContent() => new()
    {
        ["grant_type"] = GrantType,
        ["assertion"] = BuildAssertion(),
    };

    private string BuildAssertion()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var claims = new Dictionary<string, object>();
        foreach (var claim in _claims)
        {
            if (string.IsNullOrEmpty(claim.Value))
            {
                continue;
            }

            claims[claim.Key] = claim.Value;
        }

        claims.TryAdd(SubjectClaimName, _outboundMailbox);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_signingKey);

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        {
            // CryptoProviderFactory.Default caches signature providers keyed on the key's public
            // parameters, so a second token fetch with the same PEM would reuse a provider holding
            // this disposed RSA. With caching off, disposing after CreateToken returns is safe.
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };

        var tokenHandler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };

        return tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(_assertionLifetime),
            SigningCredentials = signingCredentials,
        });
    }
}
