namespace Bit.Services.Pam.Services;

/// <summary>
/// Resolves which collections the current user can Manage — the single authorization predicate for the approver
/// inbox. A user "approves" a request iff they can Manage the collection that holds the request's cipher. The list
/// endpoints use the full set as a filter; the decision/revoke endpoints check a single collection.
/// </summary>
public interface IApproverCollectionAccessQuery
{
    /// <summary>
    /// The ids of every collection the user can Manage: collections they are assigned with Manage (directly or via
    /// group), plus all collections in any organization where they are an Owner/Admin (when the org allows admin
    /// access to all collection items) or hold the EditAnyCollection permission.
    /// </summary>
    Task<HashSet<Guid>> GetManageableCollectionIdsAsync(Guid userId);

    /// <summary>Whether the user can Manage the given collection.</summary>
    Task<bool> CanManageCollectionAsync(Guid userId, Guid collectionId);
}
