using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionGroupOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionGroupOperations
{
    public static readonly CollectionGroupOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionGroupOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly CollectionGroupOperationRequirement Delete = new() { Name = nameof(Delete) };
}
