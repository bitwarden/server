using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class RoleRequirementAttribute
    : AuthorizeAttribute, IAuthorizationRequirement, IAuthorizationRequirementData
{
    public OrganizationUserType Role { get; set; }

    public RoleRequirementAttribute(OrganizationUserType type)
    {
        Role = type;
    }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}

public class RoleAuthorizationHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<RoleRequirementAttribute>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirementAttribute requirementAttribute)
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
        if (orgClaims?.Type == requirementAttribute.Role)
        {
            context.Succeed(requirementAttribute);
        }

        return Task.CompletedTask;
    }
}
