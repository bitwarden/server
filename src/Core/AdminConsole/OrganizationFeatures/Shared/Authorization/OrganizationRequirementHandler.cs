#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public interface IOrganizationRequirement : IAuthorizationRequirement;

public static class OrganizationRequirementHelpers
{
    public static Guid? GetOrganizationId(this IHttpContextAccessor httpContextAccessor)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            return null;
        }

        httpContextAccessor.HttpContext.GetRouteData().Values.TryGetValue("orgId", out var orgIdParam);
        if (!Guid.TryParse(orgIdParam?.ToString(), out var orgId))
        {
            return null;
        }

        return orgId;
    }
}
