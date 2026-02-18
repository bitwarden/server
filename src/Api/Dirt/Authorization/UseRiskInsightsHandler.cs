#nullable enable

using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Dirt.Authorization;

public class UseRiskInsightsHandler(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationRepository organizationRepository)
    : AuthorizationHandler<UseRiskInsightsRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UseRiskInsightsRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var organizationId = httpContext.GetOrganizationId();
        var organizationClaims = httpContext.User.GetCurrentContextOrganization(organizationId);

        if (organizationClaims is null)
        {
            return;
        }

        var hasAccess = organizationClaims.Type is OrganizationUserType.Owner or OrganizationUserType.Admin
                        || (organizationClaims.Type is OrganizationUserType.Custom
                            && organizationClaims.Permissions.AccessReports);

        if (!hasAccess)
        {
            return;
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null || !organization.UseRiskInsights)
        {
            return;
        }

        context.Succeed(requirement);
    }
}
