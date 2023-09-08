using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionOperations
{
    public static readonly CollectionOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionOperationRequirement Delete = new() { Name = nameof(Delete) };
    /// <summary>
    /// The operation that represents creating, updating, or removing collection access.
    /// Combined together to allow for a single requirement to be used for each operation
    /// as they all currently share the same underlying authorization logic.
    /// </summary>
    public static readonly CollectionOperationRequirement ModifyAccess = new() { Name = nameof(ModifyAccess) };
}
