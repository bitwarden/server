#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationStatusOperationsRequirement : OperationAuthorizationRequirement
{
    public NotificationStatusOperationsRequirement(string name)
    {
        Name = name;
    }
}

public static class NotificationStatusOperations
{
    public static readonly NotificationStatusOperationsRequirement Read = new(nameof(Read));
    public static readonly NotificationStatusOperationsRequirement Create = new(nameof(Create));
    public static readonly NotificationStatusOperationsRequirement Update = new(nameof(Update));
}
