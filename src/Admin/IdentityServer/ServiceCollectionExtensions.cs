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
    private const string SessionExpiredItemKey = "admin_session_expired";

    public static Tuple<IdentityBuilder, IdentityBuilder> AddPasswordlessIdentityServices<TUserStore>(
        this IServiceCollection services, GlobalSettings globalSettings, AdminSettings adminSettings)
        where TUserStore : class
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
            // Session lifetime is operator-configurable so deployments with stricter session
            // hygiene requirements can dial it down. Sliding stays on, so this is effectively
            // the idle-timeout window rather than an absolute maximum.
            options.ExpireTimeSpan = TimeSpan.FromMinutes(adminSettings.SessionTimeoutMinutes);
            options.ReturnUrlParameter = "returnUrl";
            options.SlidingExpiration = true;

            // Absolute session cap (FedRAMP AC-12): sliding renewals cannot extend a session
            // past AbsoluteSessionTimeoutMinutes from initial sign-in. Enforced against the
            // ticket's IssuedUtc, which the framework sets once at sign-in and sliding never
            // touches (unlike ExpiresUtc). Zero disables the cap.
            if (adminSettings.AbsoluteSessionTimeoutMinutes > 0)
            {
                var absoluteMax = TimeSpan.FromMinutes(adminSettings.AbsoluteSessionTimeoutMinutes);
                options.Events.OnValidatePrincipal = async ctx =>
                {
                    var issuedUtc = ctx.Properties?.IssuedUtc;
                    if (issuedUtc.HasValue && DateTimeOffset.UtcNow - issuedUtc.Value > absoluteMax)
                    {
                        // Flag the request so OnRedirectToLogin can surface a distinct
                        // "session expired" message on the login page. Without this, the
                        // user gets silently bounced to /login with no explanation.
                        ctx.HttpContext.Items[SessionExpiredItemKey] = true;
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    }
                };
            }

            // Turn the OnValidatePrincipal rejection into a user-facing message by tagging
            // the login-page redirect with error=6. Any other redirect to login (unauth'd
            // access, direct navigation) goes through untagged.
            options.Events.OnRedirectToLogin = ctx =>
            {
                if (ctx.HttpContext.Items.ContainsKey(SessionExpiredItemKey))
                {
                    var separator = ctx.RedirectUri.Contains('?') ? '&' : '?';
                    ctx.RedirectUri = $"{ctx.RedirectUri}{separator}error=6";
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
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
                // IdP/app clock drift. Reduces the window for expired-token replay. Sourced
                // from the shared constant so this tolerance can't drift apart from the
                // auth_time enforcement in LoginController.
                options.TokenValidationParameters.ClockSkew =
                    TimeSpan.FromSeconds(AdminSettings.OidcSettings.ClockSkewSeconds);

                // Force re-authentication at the IdP on every Admin Portal sign-in. A stolen
                // IdP session shouldn't automatically grant admin access without the operator
                // re-proving control (password + MFA at the IdP).
                options.AdditionalAuthorizationParameters.Add("prompt", "login");
                // Require the IdP-side authentication to have happened within max_age seconds.
                // Bounds the "attacker rides a stale IdP session into the Admin Portal" window
                // even if prompt=login is ignored by a non-conformant IdP. Server-side
                // enforcement lives in SsoSignIn (the OIDC middleware treats this as a hint).
                options.AdditionalAuthorizationParameters.Add("max_age",
                    AdminSettings.OidcSettings.MaxIdpAuthAgeSeconds.ToString(CultureInfo.InvariantCulture));

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
