using System.Net;
using Bit.Core.Context;

namespace Bit.Core.Pam.Engine;

/// <summary>
/// The request-time inputs an access rule is evaluated against: the caller's source IP and the instant the
/// evaluation is performed. <see cref="IpAddress"/> is null when the caller's address cannot be determined, which
/// IP-restricted rules treat as a denial so access never opens up on a missing signal.
/// </summary>
public sealed record AccessSignals
{
    public required IPAddress? IpAddress { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Builds the signals for the current request: the caller's source IP from
    /// <see cref="ICurrentContext.IpAddress"/> (parsed, or null when it is absent or unparseable) and the supplied
    /// evaluation <paramref name="timestamp"/>.
    /// </summary>
    public static AccessSignals From(ICurrentContext currentContext, DateTimeOffset timestamp) => new()
    {
        IpAddress = IPAddress.TryParse(currentContext.IpAddress, out var ip) ? ip : null,
        Timestamp = timestamp,
    };
}
