using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Bit.Core.Utilities;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Frame-Options
        context.Response.Headers.Add("x-frame-options", new StringValues("SAMEORIGIN"));

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-XSS-Protection
        context.Response.Headers.Add("x-xss-protection", new StringValues("1; mode=block"));

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Content-Type-Options
        context.Response.Headers.Add("x-content-type-options", new StringValues("nosniff"));

        return _next(context);
    }
}
