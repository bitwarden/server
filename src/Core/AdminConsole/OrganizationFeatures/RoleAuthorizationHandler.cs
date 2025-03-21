using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public record RoleRequirement(OrganizationUserType Role) : IAuthorizationRequirement;

public class RoleAuthorizationHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            return Task.CompletedTask;
        }

        httpContextAccessor.HttpContext.GetRouteData().Values.TryGetValue("orgId", out var orgIdParam);
        if (!Guid.TryParse(orgIdParam?.ToString(), out var orgId))
        {
            // No orgId supplied, unable to authorize
            return Task.CompletedTask;
        }

        // This could be an extension method on ClaimsPrincipal
        var orgClaims = currentContext.GetOrganization(orgId);
        if (orgClaims?.Type == requirement.Role)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
