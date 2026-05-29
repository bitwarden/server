using System.Net;
using Bit.Core.Enums;

namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleSignals
{
    public required string Username { get; init; }
    public required IPAddress IpAddress { get; init; }
    public required bool MultifactorEnabled { get; init; }
    public required DateTimeOffset UserTime { get; init; }
    public required DeviceType Device { get; init; }
}
