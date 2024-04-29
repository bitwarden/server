using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class BulkCollectionOperationRequirement : OperationAuthorizationRequirement { }

public static class BulkCollectionOperations
{
    public static readonly BulkCollectionOperationRequirement Create = new() { Name = nameof(Create) };
    /// <summary>
    /// Represents reading the Collection object. Does not include reading user and group access - see ReadAccess.
    /// </summary>
    public static readonly BulkCollectionOperationRequirement Read = new() { Name = nameof(Read) };
    /// <summary>
    /// Represents reading the user and group access to a collection.
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ReadAccess = new() { Name = nameof(ReadAccess) };
    public static readonly BulkCollectionOperationRequirement Update = new() { Name = nameof(Update) };
    /// <summary>
    /// Represents creating, updating, or removing collection access.
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ModifyAccess = new() { Name = nameof(ModifyAccess) };
    public static readonly BulkCollectionOperationRequirement Delete = new() { Name = nameof(Delete) };
    public static readonly BulkCollectionOperationRequirement ImportCiphers = new() { Name = nameof(ImportCiphers) };
}
