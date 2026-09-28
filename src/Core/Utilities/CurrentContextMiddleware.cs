using System.Runtime.CompilerServices;
using Bit.Core.Context;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

public class CurrentContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CurrentContextMiddleware> _logger;

    public CurrentContextMiddleware(RequestDelegate next, ILogger<CurrentContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext, ICurrentContext currentContext, GlobalSettings globalSettings)
    {
        await currentContext.BuildAsync(httpContext, globalSettings);
        _logger.LogInformation(
            "CurrentContextMiddleware built context (path={Path}, ctxHash={CtxHash}, hasUser={HasUser}, clientType={ClientType}, isAuth={IsAuth})",
            httpContext.Request.Path.Value,
            RuntimeHelpers.GetHashCode(currentContext),
            currentContext.UserId.HasValue,
            currentContext.IdentityClientType,
            httpContext.User?.Identity?.IsAuthenticated ?? false);
        await _next.Invoke(httpContext);
    }
}
