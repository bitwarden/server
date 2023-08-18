using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Vault.AuthorizationHandlers.Collections;

public class CollectionOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionOperations
{
    public static readonly CollectionOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionOperationRequirement Delete = new() { Name = nameof(Delete) };
}