using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class BulkCollectionOperationRequirement : OperationAuthorizationRequirement { }

public static class BulkCollectionOperations
{
    public static readonly BulkCollectionOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly BulkCollectionOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly BulkCollectionOperationRequirement ReadAccess = new() { Name = nameof(ReadAccess) };
    public static readonly BulkCollectionOperationRequirement Update = new() { Name = nameof(Update) };
    /// <summary>
    /// The operation that represents creating, updating, or removing collection access.
    /// Combined together to allow for a single requirement to be used for each operation
    /// as they all currently share the same underlying authorization logic.
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ModifyAccess = new() { Name = nameof(ModifyAccess) };
    public static readonly BulkCollectionOperationRequirement Delete = new() { Name = nameof(Delete) };
}
