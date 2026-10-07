using Bit.Api.AdminConsole.Authorization.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Api.AdminConsole.Authorization;

public static class AuthorizationHandlerCollectionExtensions
{
    public static void AddAdminConsoleAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddOrganizationAuthorization();

        // Handlers that authorize over a specific Api domain model, rather than over the organization or provider
        // on the route.
        services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IAuthorizationHandler, BulkCollectionAuthorizationHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, CollectionAuthorizationHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, OrganizationCollectionManagementAccessHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, OrgUserLinkedToUserIdHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, RecoverAccountAuthorizationHandler>(),
        ]);
    }
}
