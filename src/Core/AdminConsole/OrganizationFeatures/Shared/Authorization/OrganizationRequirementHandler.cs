#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public class OrganizationRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<IOrganizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        var organizationId = httpContextAccessor.GetOrganizationId();
        if (organizationId is null)
        {
            return;
        }

        var organization = currentContext.GetOrganization(organizationId.Value);

        var authorized = await requirement.AuthorizeAsync(organizationId.Value, organization, currentContext);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
