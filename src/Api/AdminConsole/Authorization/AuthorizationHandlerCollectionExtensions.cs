using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Authorization.Groups;
using Bit.Api.AdminConsole.Authorization.OrganizationUsers;
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

        // Fine-grained, relationship-dependent authorization for collection access is handled by plain injected
        // services rather than IAuthorizationHandler - see ICollectionAuthorizationService.
        services.TryAddScoped<ICollectionAuthorizationService, CollectionAuthorizationService>();
        services.TryAddScoped<IGroupsAuthorizationService, GroupsAuthorizationService>();
        services.TryAddScoped<IOrganizationUserAuthorizationService, OrganizationUserAuthorizationService>();
    }
}
