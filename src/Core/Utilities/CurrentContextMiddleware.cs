using Bit.Core.Context;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Utilities;

public class CurrentContextMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(
        HttpContext httpContext,
        ICurrentContext currentContext,
        GlobalSettings globalSettings
    )
    {
        await currentContext.BuildAsync(httpContext, globalSettings);
        await _next.Invoke(httpContext);
    }
}
