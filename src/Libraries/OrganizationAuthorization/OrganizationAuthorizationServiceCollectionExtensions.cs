using Bit.Api.AdminConsole.Authorization.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Api.AdminConsole.Authorization;

public static class OrganizationAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handlers that back <see cref="IOrganizationRequirement"/> and <see cref="IProviderRequirement"/>,
    /// so that any host can authorize endpoints with the requirements in this library.
    /// </summary>
    /// <remarks>
    /// The host is still responsible for registering Core's services: these handlers resolve
    /// <c>IProviderUserRepository</c> and <c>IUserService</c> from the container.
    /// </remarks>
    public static IServiceCollection AddOrganizationAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.TryAddScoped<IOrganizationContext, OrganizationContext>();

        services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IAuthorizationHandler, OrganizationRequirementHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, ProviderRequirementHandler>(),
        ]);

        return services;
    }
}
