using System.Diagnostics;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// Middleware that adds response headers and telemetry tags for propagated headers.
/// When a configured propagation header is present on the inbound request,
/// this middleware sets a corresponding response header (e.g. X-Canary → X-Canary-Routed: true)
/// and tags the active trace span for observability (DataDog/OpenTelemetry).
/// </summary>
public class HeaderPropagationResponseMiddleware
{
    private static readonly string _podName = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
    private static readonly string _gitHash = AssemblyHelpers.GetGitHash() ?? "unknown";
    private readonly RequestDelegate _next;
    private readonly string[] _headers;

    public HeaderPropagationResponseMiddleware(RequestDelegate next, string[] headers)
    {
        _next = next;
        _headers = headers;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        activity?.SetTag("pod.name", _podName);
        activity?.SetTag("build.hash", _gitHash);

        foreach (var header in _headers)
        {
            if (context.Request.Headers.TryGetValue(header, out var value)
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                activity?.SetTag(header.TrimStart('X', '-').ToLowerInvariant(), "true");
                context.Response.OnStarting(state =>
                {
                    var (ctx, h) = ((HttpContext, string))state;
                    ctx.Response.Headers[$"{h}-Routed"] = "true";
                    return Task.CompletedTask;
                }, (context, header));
            }
        }

        await _next(context);
    }
}
