#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

/// <summary>
/// Handles any requirement that implements <see cref="IOrganizationRequirement"/>.
/// Retrieves the Organization ID from the route and then passes it to the requirement's AuthorizeAsync callback to
/// determine whether the action is authorized.
/// </summary>
/// <param name="currentContext"></param>
/// <param name="httpContextAccessor"></param>
public class OrganizationRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<IOrganizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        var organizationId = httpContextAccessor.GetOrganizationId();
        if (organizationId is null)
        {
            throw new Exception("No organizationId found in route. IOrganizationRequirement cannot be used on this endpoint.");
        }

        var organization = currentContext.GetOrganization(organizationId.Value);

        var authorized = await requirement.AuthorizeAsync(organizationId.Value, organization, currentContext);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
