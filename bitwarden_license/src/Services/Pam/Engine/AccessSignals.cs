using System.Net;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// The request-time inputs an access rule is evaluated against: the caller's source IP and the instant the
/// evaluation is performed. A null <see cref="IpAddress"/> is treated by IP-restricted rules as a denial.
/// </summary>
public sealed record AccessSignals
{
    public required IPAddress? IpAddress { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Builds the signals for the current request: the caller's source IP, parsed or null, and the supplied
    /// evaluation <paramref name="timestamp"/>.
    /// </summary>
    public static AccessSignals From(string? ipAddress, DateTimeOffset timestamp) => new()
    {
        IpAddress = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
        Timestamp = timestamp,
    };
}
