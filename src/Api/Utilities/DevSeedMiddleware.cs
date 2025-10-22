using Microsoft.Extensions.Options;

namespace Bit.Api.Utilities;

public class DevSeedMiddleware
{
    private readonly RequestDelegate _next;

    public DevSeedMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IOptions<DevSeedOptions> options, IWebHostEnvironment env)
    {
        if (!context.Request.Path.StartsWithSegments("/dev/seed"))
        {
            await _next(context);
            return;
        }

        // Gate entirely by environment and explicit enable flag
        if (!env.IsDevelopment() || options.Value.Enabled != true)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Enforce shared secret header
        if (!context.Request.Headers.TryGetValue("X-Dev-Seed-Key", out var provided) ||
            string.IsNullOrWhiteSpace(options.Value.SecretKey) ||
            !string.Equals(provided.ToString(), options.Value.SecretKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
