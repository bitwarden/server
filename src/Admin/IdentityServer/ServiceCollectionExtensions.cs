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
            options.ExpireTimeSpan = TimeSpan.FromDays(2);
            options.ReturnUrlParameter = "returnUrl";
            options.SlidingExpiration = true;
        });

        return new Tuple<IdentityBuilder, IdentityBuilder>(passwordlessIdentityBuilder, regularIdentityBuilder);
    }

    public static IServiceCollection AddAdminUpstreamOidc(
        this IServiceCollection services, AdminSettings adminSettings)
    {
        if (!adminSettings.OidcEnabled)
        {
            return services;
        }

        var oidc = adminSettings.Oidc;

        services.AddAuthentication()
            .AddOpenIdConnect(AdminAuthenticationSchemes.UpstreamOidc, oidc.DisplayName, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Authority = oidc.Authority;
                options.ClientId = oidc.ClientId;
                options.ClientSecret = oidc.ClientSecret;
                options.CallbackPath = oidc.CallbackPath;
                options.SignedOutCallbackPath = oidc.SignedOutCallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = oidc.GetClaimsFromUserInfoEndpoint;
                options.MapInboundClaims = false;

                options.Scope.Clear();
                foreach (var scope in oidc.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    options.Scope.Add(scope);
                }

                options.TokenValidationParameters.NameClaimType = oidc.EmailClaimType;

                // SsoSignIn copies the OIDC principal into the Identity cookie and signs out
                // the external OIDC scheme, so by logout time the OIDC handler's own scheme
                // has no auth ticket and can't attach id_token_hint on its own. Without
                // id_token_hint (or client_id), an upstream IdP can't identify the client and falls back
                // to tenant-level Allowed Logout URLs, which rejects our app-level URL. Pull
                // the id_token from the Identity cookie (stored there via props.StoreTokens
                // during sign-in) and attach it manually.
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
