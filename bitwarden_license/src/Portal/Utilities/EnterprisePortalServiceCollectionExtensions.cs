using System;
using Bit.Core.Identity;
using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Portal.Utilities
{
    public static class EnterprisePortalServiceCollectionExtensions
    {
        public static (IdentityBuilder, IdentityBuilder) AddEnterprisePortalTokenIdentityServices(
            this IServiceCollection services)
        {
            services.TryAddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();
            var passwordlessIdentityBuilder = services.AddIdentity<User, Role>()
                .AddUserStore<UserStore>()
                .AddRoleStore<RoleStore>()
                .AddDefaultTokenProviders();

            var regularIdentityBuilder = services.AddIdentityCore<User>()
                .AddUserStore<UserStore>();

            services.TryAddScoped<EnterprisePortalTokenSignInManager, EnterprisePortalTokenSignInManager>();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/access-denied";
                options.Cookie.Name = $"Bitwarden_BusinessPortal";
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(2);
                options.ReturnUrlParameter = "returnUrl";
                options.SlidingExpiration = true;
            });

            return (passwordlessIdentityBuilder, regularIdentityBuilder);
        }
    }
}
