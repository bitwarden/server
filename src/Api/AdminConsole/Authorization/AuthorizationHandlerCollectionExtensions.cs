using Bit.Api.AdminConsole.Authorization.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Api.AdminConsole.Authorization;

public static class AuthorizationHandlerCollectionExtensions
{
    public static void AddAdminConsoleAuthorizationHandlers(this IServiceCollection services)
    {
        // The generic organization and provider requirement handlers live in the OrganizationAuthorization library
        // so that hosts which cannot reference Api (such as Pam) can use the same requirements.
        services.AddOrganizationAuthorization();

        // Resource-based handlers stay here: they authorize over specific Api domain models rather than
        // over the organization or provider on the route.
        services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IAuthorizationHandler, BulkCollectionAuthorizationHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, CollectionAuthorizationHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, OrganizationCollectionManagementAccessHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, OrgUserLinkedToUserIdHandler>(),
            ServiceDescriptor.Scoped<IAuthorizationHandler, RecoverAccountAuthorizationHandler>(),
        ]);
    }
}
