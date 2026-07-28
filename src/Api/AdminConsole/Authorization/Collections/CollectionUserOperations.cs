using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionUserOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionUserOperations
{
    public static readonly CollectionUserOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionUserOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly CollectionUserOperationRequirement Delete = new() { Name = nameof(Delete) };
}
