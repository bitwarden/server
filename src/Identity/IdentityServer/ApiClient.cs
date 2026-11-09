using Bit.Identity.IdentityServer.RequestValidators;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer;

public class ApiClient : Client
{
    public ApiClient(ApiClientConfiguration config)
    {
        ClientId = config.Id;
        AllowedGrantTypes = new[] { GrantType.ResourceOwnerPassword, GrantType.AuthorizationCode, WebAuthnGrantValidator.GrantType };

        RefreshTokenExpiration = config.ApplyAbsoluteExpirationOnRefreshToken
            ? TokenExpiration.Absolute
            : TokenExpiration.Sliding;
        RefreshTokenUsage = TokenUsage.ReUse;
        SlidingRefreshTokenLifetime = config.SlidingRefreshTokenLifetimeSecondsOverride ?? (86400 * config.RefreshTokenSlidingDays);
        AbsoluteRefreshTokenLifetime = config.AbsoluteRefreshTokenLifetimeSeconds ?? 0;

        UpdateAccessTokenClaimsOnRefresh = true;
        AccessTokenLifetime = config.AccessTokenLifetimeSeconds;
        AllowOfflineAccess = true;

        RequireConsent = false;
        RequirePkce = true;
        RequireClientSecret = false;

        if (config.RedirectUris != null)
        {
            RedirectUris = config.RedirectUris;
        }
        if (config.PostLogoutRedirectUris != null)
        {
            PostLogoutRedirectUris = config.PostLogoutRedirectUris;
        }
        if (config.AllowedCorsOrigins != null)
        {
            AllowedCorsOrigins = config.AllowedCorsOrigins;
        }

        AllowedScopes = config.Scopes ?? new[] { "api" };
    }
}
