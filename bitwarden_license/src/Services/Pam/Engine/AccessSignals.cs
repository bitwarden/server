using System.Net;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// The request-time inputs an access rule is evaluated against. <see cref="IpAddress"/> is null when the caller's
/// address cannot be determined, which IP-restricted rules treat as a denial so access never opens up on a missing
/// signal.
/// </summary>
public sealed record AccessSignals
{
    public required IPAddress? IpAddress { get; init; }

    /// <summary>
    /// Builds the signals for the current request: the caller's source IP, parsed, or null when it is absent or
    /// unparseable. Callers typically pass the request's source address (e.g. <c>ICurrentContext.IpAddress</c>).
    /// </summary>
    public static AccessSignals From(string? ipAddress) => new()
    {
        IpAddress = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
    };
}
