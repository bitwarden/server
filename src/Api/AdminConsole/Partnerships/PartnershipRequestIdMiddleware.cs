namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Adds the <see cref="PartnershipRequestId.HeaderName"/> header to every response on a partnership endpoint path.
/// </summary>
public sealed class PartnershipRequestIdMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(PartnershipRoutes.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers[PartnershipRequestId.HeaderName] = PartnershipRequestId.Get(context);
        }

        return next(context);
    }
}
