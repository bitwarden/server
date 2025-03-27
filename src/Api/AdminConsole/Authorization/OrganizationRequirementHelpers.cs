#nullable enable

namespace Bit.Api.AdminConsole.Authorization;

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
