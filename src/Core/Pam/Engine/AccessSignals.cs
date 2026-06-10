using System.Net;

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
}
