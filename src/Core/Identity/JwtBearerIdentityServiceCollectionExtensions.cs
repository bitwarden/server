using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Identity
{
    public static class JwtBearerIdentityServiceCollectionExtensions
    {
        public static IdentityBuilder AddJwtBearerIdentity(
            this IServiceCollection services)
        {
            return services.AddJwtBearerIdentity(setupAction: null, jwtBearerSetupAction: null);
        }

        public static IdentityBuilder AddJwtBearerIdentity(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction,
            Action<JwtBearerIdentityOptions> jwtBearerSetupAction)
        {
            // Services used by identity
            services.AddOptions();
            services.AddAuthentication();

            // Hosting doesn't add IHttpContextAccessor by default
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            // Identity services
            services.TryAddSingleton<IdentityMarkerService>();
            services.TryAddScoped<IUserValidator<User>, UserValidator<User>>();
            services.TryAddScoped<IPasswordValidator<User>, PasswordValidator<User>>();
            services.TryAddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();
            services.TryAddScoped<IRoleValidator<Role>, RoleValidator<Role>>();
            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<User>>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<User>, UserClaimsPrincipalFactory<User, Role>>();
            services.TryAddScoped<UserManager<User>, UserManager<User>>();
            services.TryAddScoped<JwtBearerSignInManager, JwtBearerSignInManager>();
            services.TryAddScoped<RoleManager<Role>, RoleManager<Role>>();

            if(setupAction != null)
            {
                services.Configure(setupAction);
            }

            if(jwtBearerSetupAction != null)
            {
                services.Configure(jwtBearerSetupAction);
            }

            return new IdentityBuilder(typeof(User), typeof(Role), services);
        }
    }
}
