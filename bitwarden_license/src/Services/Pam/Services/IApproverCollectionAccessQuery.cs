namespace Bit.Services.Pam.Services;

/// <summary>
/// Resolves which collections a user can Manage: a user may approve a request iff they can Manage its
/// collection.
/// </summary>
public interface IApproverCollectionAccessQuery
{
    /// <summary>
    /// The ids of every collection the user can Manage: assigned directly or via group, plus every collection
    /// in an organization where they are an Owner/Admin with all-collection access, or hold EditAnyCollection.
    /// </summary>
    Task<HashSet<Guid>> GetManageableCollectionIdsAsync(Guid userId);

    /// <summary>Whether the user can Manage the given collection.</summary>
    Task<bool> CanManageCollectionAsync(Guid userId, Guid collectionId);
}
