namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// The per-request identifier returned to partners in the <see cref="HeaderName"/> header and in every error body.
/// </summary>
public static class PartnershipRequestId
{
    public const string HeaderName = "X-Request-Id";

    /// <summary>
    /// Returns the server-assigned identifier for the current request. Never taken from client input.
    /// </summary>
    public static string Get(HttpContext httpContext) => httpContext.TraceIdentifier;
}
