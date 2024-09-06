#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationStatusOperationsRequirement : OperationAuthorizationRequirement;

public class NotificationStatusOperations
{
    public static readonly NotificationStatusOperationsRequirement Read = new() { Name = nameof(Read) };
    public static readonly NotificationStatusOperationsRequirement Create = new() { Name = nameof(Create) };
    public static readonly NotificationStatusOperationsRequirement Update = new() { Name = nameof(Update) };
}
