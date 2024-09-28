#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationOperationsRequirement : OperationAuthorizationRequirement
{
    public NotificationOperationsRequirement(string name)
    {
        Name = name;
    }
}

public static class NotificationOperations
{
    public static readonly NotificationOperationsRequirement Read = new(nameof(Read));
    public static readonly NotificationOperationsRequirement Create = new(nameof(Create));
    public static readonly NotificationOperationsRequirement Update = new(nameof(Update));
}
