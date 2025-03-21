#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public interface IOrganizationRequirement : IAuthorizationRequirement;

public abstract class OrganizationRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<IOrganizationRequirement>
{
    protected abstract Task<bool> HandleOrganizationRequirementAsync(IOrganizationRequirement requirement, Guid organizationId, CurrentContextOrganization? organization);

    protected async Task<bool> IsProviderForOrganizationAsync(Guid organizationId) =>
        await currentContext.ProviderUserForOrgAsync(organizationId);

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            return;
        }

        httpContextAccessor.HttpContext.GetRouteData().Values.TryGetValue("orgId", out var orgIdParam);
        if (!Guid.TryParse(orgIdParam?.ToString(), out var orgId))
        {
            // No orgId supplied, unable to authorize
            return;
        }

        var organization = currentContext.GetOrganization(orgId);

        var authorized = await HandleOrganizationRequirementAsync(requirement, orgId, organization);
        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
