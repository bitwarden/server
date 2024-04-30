using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class BulkCollectionOperationRequirement : OperationAuthorizationRequirement { }

public static class BulkCollectionOperations
{
    /// <summary>
    /// Create a new collection
    /// </summary>
    public static readonly BulkCollectionOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly BulkCollectionOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly BulkCollectionOperationRequirement ReadAccess = new() { Name = nameof(ReadAccess) };
    public static readonly BulkCollectionOperationRequirement ReadWithAccess = new() { Name = nameof(ReadWithAccess) };
    /// <summary>
    /// Update a collection, including its user and group associations
    /// </summary>
    public static readonly BulkCollectionOperationRequirement Update = new() { Name = nameof(Update) };
    /// <summary>
    /// Delete a collection
    /// </summary>
    public static readonly BulkCollectionOperationRequirement Delete = new() { Name = nameof(Delete) };
    /// <summary>
    /// Import ciphers into a collection
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ImportCiphers = new() { Name = nameof(ImportCiphers) };
    /// <summary>
    /// Create, update or delete Collection-User associations
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ModifyUserAccess = new() { Name = nameof(ModifyUserAccess) };
    /// <summary>
    /// Create, update or delete Collection-Group associations
    /// </summary>
    public static readonly BulkCollectionOperationRequirement ModifyGroupAccess = new() { Name = nameof(ModifyGroupAccess) };
}
