using System;
using Microsoft.AspNet.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bit.Core.Domains;

namespace Bit.Core.Identity
{
    public static class JwtBearerIdentityServiceCollectionExtensions
    {
        public static IdentityBuilder AddJwtBearerIdentit(
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
