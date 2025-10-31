using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Api.AdminConsole.Authorization;

public static class AuthorizationHandlerCollectionExtensions
{
    public static void AddAdminConsoleAuthorizationHandlers(this IServiceCollection services)
    {
        services.TryAddScoped<IOrganizationContext, OrganizationContext>();

        services.TryAddEnumerable([
                ServiceDescriptor.Scoped<IAuthorizationHandler, BulkCollectionAuthorizationHandler>(),
                ServiceDescriptor.Scoped<IAuthorizationHandler, CollectionAuthorizationHandler>(),
                ServiceDescriptor.Scoped<IAuthorizationHandler, GroupAuthorizationHandler>(),
                ServiceDescriptor.Scoped<IAuthorizationHandler, OrganizationRequirementHandler>(),
                ServiceDescriptor.Scoped<IAuthorizationHandler, RecoverAccountAuthorizationHandler>(),
            ]);
    }
}
