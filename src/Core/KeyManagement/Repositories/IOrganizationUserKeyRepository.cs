using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Repositories;

/// <summary>
/// Reads and writes the key material an organization holds for its members.
/// </summary>
public interface IOrganizationUserKeyRepository
{
    /// <summary>
    /// Reads memberships in the organization that still have a V2 upgrade token, that is, members whose account
    /// recovery key an admin has not yet re-wrapped with the member's V2 user key.
    /// </summary>
    /// <remarks>
    /// The memberships are ordered by id. To read the next page, pass the id of the last membership of the previous
    /// page as <paramref name="afterId"/>.
    /// </remarks>
    /// <param name="organizationId">The organization the memberships belong to.</param>
    /// <param name="includeOwners">Whether memberships of the Owner type are returned.</param>
    /// <param name="afterId">Only memberships with an id after this one are returned. Null starts from the first.</param>
    /// <param name="maxCount">The maximum number of memberships to return.</param>
    Task<ICollection<OrganizationUserV2UpgradeDetails>> GetManyPendingV2UpgradesByOrganizationIdAsync(
        Guid organizationId, bool includeOwners, Guid? afterId, int maxCount);

    /// <summary>
    /// Replaces the account recovery keys of the given memberships and clears their V2 upgrade tokens. An update
    /// with a null key clears the account recovery key too, which unenrolls the member.
    /// </summary>
    /// <remarks>
    /// A row is written only when it belongs to the organization, still has a V2 upgrade token, is still enrolled in
    /// account recovery, and its user row still holds the key id given in the update. An Owner is written only when
    /// <paramref name="includeOwners"/> is true. A row that fails these conditions is skipped, and the rest are still
    /// written.
    /// </remarks>
    /// <param name="organizationId">The organization the memberships belong to.</param>
    /// <param name="includeOwners">Whether memberships of the Owner type are written.</param>
    /// <param name="updates">The re-wrapped account recovery keys.</param>
    /// <param name="revisionDate">The revision date to set on the updated memberships.</param>
    /// <returns>The ids of the memberships that were updated.</returns>
    Task<ICollection<Guid>> UpdateManyV2UpgradedAccountRecoveryKeysAsync(Guid organizationId, bool includeOwners,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates, DateTime revisionDate);
}
