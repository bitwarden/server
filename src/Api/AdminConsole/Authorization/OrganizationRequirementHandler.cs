#nullable enable

using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Handles any requirement that implements <see cref="IOrganizationRequirement"/>.
/// Retrieves the Organization ID from the route and then passes it to the requirement's AuthorizeAsync callback to
/// determine whether the action is authorized.
/// </summary>
public class OrganizationRequirementHandler(
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<IOrganizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        var organizationId = httpContextAccessor.GetOrganizationId();

        var organizationClaims = context.User.GetCurrentContextOrganization(organizationId);
        var providerOrganizationContext = null; // TODO

        var authorized = await requirement.AuthorizeAsync(organizationId, organizationClaims, providerOrganizationContext);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
