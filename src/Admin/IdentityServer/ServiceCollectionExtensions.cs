using System.Globalization;
using Bit.Admin.Auth.IdentityServer;
using Bit.Core.Auth.Identity;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bit.Admin.IdentityServer;

public static class ServiceCollectionExtensions
{
    public static Tuple<IdentityBuilder, IdentityBuilder> AddPasswordlessIdentityServices<TUserStore>(
        this IServiceCollection services, GlobalSettings globalSettings) where TUserStore : class
    {
        services.TryAddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(15);
        });

        var passwordlessIdentityBuilder = services.AddIdentity<IdentityUser, Role>()
            .AddUserStore<TUserStore>()
            .AddRoleStore<RoleStore>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>();

        var regularIdentityBuilder = services.AddIdentityCore<User>()
            .AddUserStore<UserStore>();

        services.TryAddScoped<PasswordlessSignInManager<IdentityUser>, PasswordlessSignInManager<IdentityUser>>();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/";
            options.AccessDeniedPath = "/login?accessDenied=true";
            options.Cookie.Name = $"Bitwarden_{globalSettings.ProjectName}";
            options.Cookie.HttpOnly = true;
            // Always mark Secure. Default (SameAsRequest) can downgrade behind a TLS-terminating
            // proxy that doesn't forward X-Forwarded-Proto, causing the cookie to leak over HTTP.
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Lax is required so the cookie is sent on the top-level GET redirect back from the
            // OIDC callback; Strict would break SSO. HttpOnly + Secure + Lax is the standard
            // defense-in-depth combo.
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(2);
            options.ReturnUrlParameter = "returnUrl";
            options.SlidingExpiration = true;
        });

        return new Tuple<IdentityBuilder, IdentityBuilder>(passwordlessIdentityBuilder, regularIdentityBuilder);
    }

    public static IServiceCollection AddAdminUpstreamOidc(
        this IServiceCollection services, AdminSettings adminSettings)
    {
        // Fail-closed: if the operator hasn't supplied Authority/ClientId/ClientSecret, we don't
        // register the handler at all. The SSO controller actions then return NotFound on any hit.
        if (!adminSettings.OidcEnabled)
        {
            return services;
        }

        var oidc = adminSettings.Oidc;

        services.AddAuthentication()
            .AddOpenIdConnect(AdminAuthenticationSchemes.UpstreamOidc, oidc.DisplayName, options =>
            {
                // Stage the OIDC principal into ASP.NET Identity's short-lived external cookie.
                // SsoSignIn reads it, does the allowlist check, then promotes to the application
                // cookie via SignInWithClaimsAsync. Separates "who the IdP says you are" from
                // "who our app treats you as".
                options.SignInScheme = IdentityConstants.ExternalScheme;

                // OIDC discovery root; the handler fetches /.well-known/openid-configuration
                // to resolve authorization/token/userinfo/JWKS endpoints.
                options.Authority = oidc.Authority;

                // Fail-loud on http:// discovery in every environment. The framework default is
                // true except in Development, so a mis-set ASPNETCORE_ENVIRONMENT in production
                // could silently allow an on-path attacker to swap the JWKS. Explicit here.
                options.RequireHttpsMetadata = true;

                // Client credentials for the token exchange (client_secret_post).
                options.ClientId = oidc.ClientId;
                options.ClientSecret = oidc.ClientSecret;

                // Where the IdP posts the authorization code and where it redirects after
                // RP-initiated logout. Must match the IdP's Allowed Callback/Logout URL lists
                // exactly (including any app PathBase, e.g. /admin for self-hosted).
                options.CallbackPath = oidc.CallbackPath;
                options.SignedOutCallbackPath = oidc.SignedOutCallbackPath;

                // Authorization Code + PKCE. The modern, secure default for confidential clients
                // and the only variant most current IdPs recommend. PKCE closes the auth-code
                // interception window regardless of client type.
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                // Persist the access/refresh/id tokens in the auth ticket so downstream code
                // can retrieve them via HttpContext.GetTokenAsync. We need id_token later to
                // attach it as id_token_hint on RP-initiated logout.
                options.SaveTokens = true;

                // Always hit UserInfo after the token exchange. Some IdPs (Okta) leave email
                // and email_verified out of the ID token; UserInfo is the authoritative source.
                // Extra round-trip is negligible on interactive admin logins.
                options.GetClaimsFromUserInfoEndpoint = true;

                // Disable the legacy WS-* claim-name mapping so `sub`, `email`, `email_verified`
                // stay in their OIDC-native short form. Matches the JWT on the wire and simplifies
                // config (`EmailClaimType=email` rather than the long xmlsoap URI).
                options.MapInboundClaims = false;

                options.Scope.Clear();
                foreach (var scope in oidc.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    options.Scope.Add(scope);
                }

                // Which claim is treated as Identity.Name on the principal. Set to the email
                // claim so User.Identity.Name is the operator's email address rather than `sub`.
                options.TokenValidationParameters.NameClaimType = oidc.EmailClaimType;

                // Lock in the token validation posture explicitly. Framework defaults already
                // enable each of these, but implicit defaults can shift across major .NET
                // versions or get silently downgraded by config binding. Fail-loud instead.
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.RequireSignedTokens = true;
                options.TokenValidationParameters.RequireExpirationTime = true;
                // Tighter than the 5-minute default; still permissive enough to survive typical
                // IdP/app clock drift. Reduces the window for expired-token replay.
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);

                // Force re-authentication at the IdP on every Admin Portal sign-in. A stolen
                // IdP session shouldn't automatically grant admin access without the operator
                // re-proving control (password + MFA at the IdP).
                options.AdditionalAuthorizationParameters.Add("prompt", "login");
                // Require the IdP-side authentication to have happened within max_age seconds.
                // Bounds the "attacker rides a stale IdP session into the Admin Portal" window
                // even if prompt=login is ignored by a non-conformant IdP. Server-side
                // enforcement lives in SsoSignIn (the OIDC middleware treats this as a hint).
                options.AdditionalAuthorizationParameters.Add("max_age",
                    AdminAuthenticationSchemes.MaxIdpAuthAgeSeconds.ToString(CultureInfo.InvariantCulture));

                // SsoSignIn copies the OIDC principal into the Identity cookie and signs out
                // the external OIDC scheme, so by logout time the OIDC handler's own scheme has
                // no auth ticket and can't attach id_token_hint on its own. Without id_token_hint
                // (or client_id), an upstream IdP can't identify the client and falls back to
                // tenant-level Allowed Logout URLs, which rejects our app-level URL. Pull the
                // id_token from the Identity cookie (stored there via props.StoreTokens during
                // sign-in) and attach it manually.
                options.Events.OnRedirectToIdentityProviderForSignOut = async ctx =>
                {
                    var idToken = await ctx.HttpContext.GetTokenAsync("id_token");
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        ctx.ProtocolMessage.IdTokenHint = idToken;
                    }
                };
            });

        return services;
    }
}
