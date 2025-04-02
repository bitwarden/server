﻿#nullable enable

namespace Bit.Api.AdminConsole.Authorization;

public static class OrganizationRequirementHelpers
{
    public static Guid GetOrganizationId(this HttpContext httpContext)
    {
        httpContext.GetRouteData().Values.TryGetValue("orgId", out var orgIdParam);
        if (orgIdParam == null || !Guid.TryParse(orgIdParam.ToString(), out var orgId))
        {
            throw new InvalidOperationException(
                "A route decorated with with '[Authorize<Requirement>]' should include a route value named 'orgId' either through the [Controller] attribute or through a '[Http*]' attribute.");
        }

        return orgId;
    }
}
