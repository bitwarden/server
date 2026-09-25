using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Repositories;

/// <summary>
/// Reads and writes the key material an organization holds for its members.
/// </summary>
public interface IOrganizationUserKeyRepository
{
    /// <summary>
    /// Reads every membership in the organization that still has a V2 upgrade token, that is, every member whose
    /// account recovery key an admin has not yet re-wrapped with the member's V2 user key.
    /// </summary>
    Task<ICollection<OrganizationUserV2UpgradeDetails>> GetManyPendingV2UpgradesByOrganizationIdAsync(
        Guid organizationId);

    /// <summary>
    /// Replaces the account recovery keys of the given memberships and clears their V2 upgrade tokens. An update
    /// with a null key clears the account recovery key too, which unenrolls the member.
    /// </summary>
    /// <remarks>
    /// Every row is written, or none of them are. A row is written only when it belongs to the organization, still
    /// has a V2 upgrade token, and its user row still holds the key id given in the update. If any row fails these
    /// conditions, nothing is written and the result is 0.
    /// </remarks>
    /// <returns>The number of rows written.</returns>
    Task<int> UpdateManyV2UpgradedAccountRecoveryKeysAsync(Guid organizationId,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates);
}
