#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationOperationsRequirement : OperationAuthorizationRequirement;

public class NotificationOperations
{
    public static readonly NotificationOperationsRequirement Read = new() { Name = nameof(Read) };
    public static readonly NotificationOperationsRequirement Create = new() { Name = nameof(Create) };
    public static readonly NotificationOperationsRequirement Update = new() { Name = nameof(Update) };
}
