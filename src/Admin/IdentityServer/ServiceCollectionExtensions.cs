using Bit.Admin.Auth.IdentityServer;
using Bit.Core.Auth.Identity;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bit.Admin.IdentityServer;

public static class ServiceCollectionExtensions
{
    public static Tuple<IdentityBuilder, IdentityBuilder> AddPasswordlessIdentityServices<TUserStore>(
        this IServiceCollection services, GlobalSettings globalSettings, AdminSettings adminSettings)
        where TUserStore : class
    {
        // SessionTimeoutMinutes = 0 (or negative) sets ExpireTimeSpan = Zero and locks every
        // admin out on the next request with no error message. Fail loudly so a typo in the
        // env var doesn't brick the portal.
        if (adminSettings.SessionTimeoutMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"AdminSettings:SessionTimeoutMinutes must be greater than 0 " +
                $"(got {adminSettings.SessionTimeoutMinutes}).");
        }

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
            // Scope Secure=Always to SSO deployments only. Self-hosters who declined TLS at
            // install time reach the Admin over plain HTTP; forcing Secure there would silently
            // drop the cookie and lock them out with no error. When SSO is enabled the OIDC
            // correlation cookie (SameSite=None) already requires HTTPS end-to-end, so upgrading
            // the app cookie to match is safe. SameAsRequest is fine in the non-SSO topology -
            // Startup calls UseForwardedHeaders(XForwardedProto) so a TLS-terminating proxy
            // resolves to https correctly.
            options.Cookie.SecurePolicy = adminSettings.OidcEnabled
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            // Lax is required so the cookie is sent on the top-level GET redirect back from the
            // OIDC callback; Strict would break SSO. HttpOnly + Secure + Lax is the standard
            // defense-in-depth combo.
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Session lifetime and sliding-vs-absolute behavior are both operator-configurable.
            // With SessionSliding = true (default), this is an idle timeout. With false, it's a
            // fixed lifetime from sign-in - the framework's built-in absolute cap. FedRAMP-strict
            // deployments should set SessionSliding=false + short SessionTimeoutMinutes + rely on
            // SSO (which lets the IdP silently re-sign-in when the IdP session is still fresh).
            options.ExpireTimeSpan = TimeSpan.FromMinutes(adminSettings.SessionTimeoutMinutes);
            options.SlidingExpiration = adminSettings.SessionSliding;
            options.ReturnUrlParameter = "returnUrl";
        });

        // Show a distinct "session expired" message on the login page whenever the browser sent
        // our auth cookie but the framework rejected it (idle or absolute expiry). Cookie in
        // request but user unauth'd = they had a session that just died, not a first-time
        // visitor. Runs as PostConfigure so it can chain the default OnRedirectToLogin rather
        // than replace it (Identity's cookie configuration is a PostConfigure too).
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            var existingRedirect = options.Events.OnRedirectToLogin;
            options.Events.OnRedirectToLogin = ctx =>
            {
                var hadCookie = !string.IsNullOrEmpty(options.Cookie.Name)
                    && ctx.HttpContext.Request.Cookies.ContainsKey(options.Cookie.Name);
                if (hadCookie)
                {
                    var separator = ctx.RedirectUri.Contains('?') ? '&' : '?';
                    ctx.RedirectUri = $"{ctx.RedirectUri}{separator}error=6";
                }
                return existingRedirect(ctx);
            };
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

                // Disable the legacy WS-* claim-name mapping so `sub`, `email`, `email_verified`
                // stay in their OIDC-native short form. Matches the JWT on the wire and simplifies
                // config (`EmailClaimType=email` rather than the long xmlsoap URI).
                //
                // We deliberately do NOT call GetClaimsFromUserInfoEndpoint. UserInfo claims go
                // through a separate ClaimActions pipeline that MapInboundClaims does not
                // affect and whose defaults reintroduce the xmlsoap URIs - untangling that
                // requires additional per-claim mapping. Rely on the ID token instead: it's a
                // one-checkbox change at every mainstream IdP to include `email` and
                // `email_verified` in the ID token, and it's already the default at Auth0,
                // Azure AD/Entra, and Keycloak (Okta needs a small config change).
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
                options.TokenValidationParameters.ClockSkew =
                    TimeSpan.FromSeconds(AdminSettings.OidcSettings.ClockSkewSeconds);

                // Force credential entry at the IdP on every Admin Portal sign-in. The Admin
                // Portal is a high-privilege surface, and re-proving control (password + MFA)
                // on each cookie renewal is worth the UX cost - a stolen IdP session cookie
                // shouldn't grant admin access silently. Guarantees auth_time on the returned
                // ID token is effectively "now", so we don't also need max_age.
                options.AdditionalAuthorizationParameters.Add("prompt", "login");

                // Pin the OIDC helper cookies explicitly (default is SameSite=None, which the
                // framework requires for the redirect-back cross-site POST). SecurePolicy is
                // safe as Always because the OIDC handler is only registered when OidcEnabled
                // is true, and RequireHttpsMetadata above already refuses to run over http://.
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = SameSiteMode.None;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

                // Attach id_token_hint from the sign-out AuthenticationProperties. Logout
                // reads the id_token from the app cookie up-front (before SignOutAsync) and
                // passes it here as a stored token, so this doesn't rely on handler-level
                // caching of the pre-sign-out ticket. Without id_token_hint (or client_id),
                // an upstream IdP can't identify the client and falls back to tenant-level
                // Allowed Logout URLs, which rejects our app-level URL.
                options.Events.OnRedirectToIdentityProviderForSignOut = ctx =>
                {
                    var idToken = ctx.Properties?.GetTokenValue("id_token");
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        ctx.ProtocolMessage.IdTokenHint = idToken;
                    }
                    return Task.CompletedTask;
                };
            });

        return services;
    }
}
