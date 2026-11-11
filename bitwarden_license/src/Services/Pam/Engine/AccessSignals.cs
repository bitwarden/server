using System.Net;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// The request-time inputs an access rule is evaluated against. IP-restricted rules deny a null
/// <see cref="IpAddress"/>.
/// </summary>
public sealed record AccessSignals
{
    public required IPAddress? IpAddress { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public static AccessSignals From(string? ipAddress, DateTimeOffset timestamp) => new()
    {
        IpAddress = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
        Timestamp = timestamp,
    };
}
