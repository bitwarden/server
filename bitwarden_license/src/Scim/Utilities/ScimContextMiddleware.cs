using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Scim.Context;

namespace Bit.Scim.Utilities;

public class ScimContextMiddleware
{
    private readonly RequestDelegate _next;

    public ScimContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(
        HttpContext httpContext,
        IScimContext scimContext,
        GlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IOrganizationConnectionRepository organizationConnectionRepository
    )
    {
        await scimContext.BuildAsync(
            httpContext,
            globalSettings,
            organizationRepository,
            organizationConnectionRepository
        );
        await _next.Invoke(httpContext);
    }
}
