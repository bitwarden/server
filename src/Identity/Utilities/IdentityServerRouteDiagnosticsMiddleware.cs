using Microsoft.AspNetCore.Http.Features;
using IdentityServerEndpoint = Duende.IdentityServer.Hosting.Endpoint;

namespace Bit.Identity.Utilities;

/// <summary>
/// Tags requests handled by IdentityServer protocol endpoints with the <c>http.route</c> attribute.
/// </summary>
/// <remarks>
/// IdentityServer handles protocol requests in middleware instead of through ASP.NET Core endpoint routing, so
/// neither the request activity nor the <c>http.server.request.duration</c> metric get a route. Tracing backends
/// that build span names from <c>http.request.method</c> and <c>http.route</c> then collapse every protocol request
/// into just its method (e.g. <c>POST</c>). Must be registered immediately before <c>UseIdentityServer()</c> with
/// <see cref="MarkUnhandled"/> registered immediately after it.
/// </remarks>
public sealed class IdentityServerRouteDiagnosticsMiddleware
{
    private const string HttpRouteTag = "http.route";
    private static readonly object _unhandledKey = new();

    private readonly RequestDelegate _next;
    private readonly IdentityServerEndpoint[] _endpoints;

    public IdentityServerRouteDiagnosticsMiddleware(RequestDelegate next, IEnumerable<IdentityServerEndpoint> endpoints)
    {
        _next = next;
        _endpoints = endpoints.ToArray();
    }

    public async Task Invoke(HttpContext context)
    {
        var endpoint = Array.Find(_endpoints, e => e.IsMatch(context));

        await _next(context);

        // IdentityServer passes requests for disabled endpoints down the pipeline; those aren't its routes.
        if (endpoint is null || context.Items.ContainsKey(_unhandledKey))
        {
            return;
        }

        // Use the registered path rather than the request path to keep the attribute low cardinality.
        var route = endpoint.Path.Value;
        context.Features.Get<IHttpActivityFeature>()?.Activity.SetTag(HttpRouteTag, route);
        context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new(HttpRouteTag, route));
    }

    /// <summary>
    /// Marks a request as not handled by IdentityServer. Must be registered immediately after <c>UseIdentityServer()</c>.
    /// </summary>
    public static Task MarkUnhandled(HttpContext context, RequestDelegate next)
    {
        context.Items[_unhandledKey] = true;
        return next(context);
    }
}
