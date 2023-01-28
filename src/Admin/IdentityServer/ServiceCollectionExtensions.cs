using Bit.Core.Entities;
using Bit.Core.Identity;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            .AddDefaultTokenProviders();

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
}
