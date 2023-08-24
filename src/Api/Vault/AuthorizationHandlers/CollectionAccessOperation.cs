using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers;

public class CollectionAccessOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionAccessOperation
{
    /// <summary>
    /// The operation that represents creating, updating, or removing collection access.
    /// Combined together to allow for a single requirement to be used for each operation
    /// as they all currently share the same underlying authorization logic.
    /// </summary>
    public static readonly CollectionAccessOperationRequirement CreateUpdateDelete = new() { Name = nameof(CreateUpdateDelete) };
}
