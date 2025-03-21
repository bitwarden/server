#nullable enable

using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public abstract class OrganizationRequirementAttribute
    : AuthorizeAttribute, IAuthorizationRequirement, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [this];
}

public abstract class OrganizationRequirementHandler : AuthorizationHandler<OrganizationRequirementAttribute>
{
    protected Guid? OrganizationId { get; set; }
    protected CurrentContextOrganization? Organization { get; set; }

    protected OrganizationRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
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

        OrganizationId = orgId;
        if (OrganizationId.HasValue)
        {
            Organization = currentContext.GetOrganization(OrganizationId.Value);
        }
    }
}

public class ManageUsersRequirementAttribute : OrganizationRequirementAttribute;

public class AdminConsoleRequirementsHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : OrganizationRequirementHandler(currentContext, httpContextAccessor)
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationRequirementAttribute requirement)
    {
        var authorized = requirement switch
        {
            ManageUsersRequirementAttribute => Organization is
            { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
            { Permissions.ManageUsers: true },
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
