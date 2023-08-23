using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers;

public class CollectionAccessOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionAccessOperation
{
    /// <summary>
    /// The operation that represents creating or removing collection access.
    /// Combined together to allow for a single requirement to be used for both operations as they
    /// currently share the same underlying authorization logic.
    /// </summary>
    public static readonly CollectionAccessOperationRequirement CreateDelete = new() { Name = nameof(CreateDelete) };
}
