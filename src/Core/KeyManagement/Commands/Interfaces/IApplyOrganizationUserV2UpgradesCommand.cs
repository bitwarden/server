using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface IApplyOrganizationUserV2UpgradesCommand
{
    /// <summary>
    /// Replaces the account recovery keys of members who upgraded to V2 encryption, and clears the V2 upgrade
    /// tokens used for the re-wrap. An update with a null key unenrolls the member from account recovery, which
    /// is how an admin clears an upgrade that cannot be completed.
    /// </summary>
    /// <remarks>
    /// Each update is checked against the key id on the member's user row, so an admin cannot install a key
    /// wrapped against a user key the member no longer holds. An update that fails this check is skipped rather
    /// than rejected, and the membership stays pending. Each member who is unenrolled gets a withdrawal event.
    /// </remarks>
    /// <param name="organizationId">The organization the memberships belong to.</param>
    /// <param name="updates">The re-wrapped account recovery keys.</param>
    Task ApplyAsync(Guid organizationId, IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates);
}
