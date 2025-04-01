#nullable enable

namespace Bit.Api.AdminConsole.Authorization;

public static class OrganizationRequirementHelpers
{
    public static Guid GetOrganizationId(this IHttpContextAccessor httpContextAccessor)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            throw new InvalidOperationException("This method should only be called in the context of an HTTP Request.");
        }

        httpContextAccessor.HttpContext.GetRouteData().Values.TryGetValue("orgId", out var orgIdParam);
        if (orgIdParam == null || !Guid.TryParse(orgIdParam.ToString(), out var orgId))
        {
            throw new InvalidOperationException(
                "A route decorated with with '[Authorize<Requirement>]' should include a route value named 'orgId' either through the [Controller] attribute or through a '[Http*]' attribute.");
        }

        return orgId;
    }
}
