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
    /// Every update is applied, or none of them are. Each update is checked against the key id on the member's
    /// user row before anything is written, so an admin cannot install a key wrapped against a user key the
    /// member no longer holds.
    /// </remarks>
    /// <param name="organizationId">The organization the memberships belong to.</param>
    /// <param name="updates">The re-wrapped account recovery keys.</param>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// Thrown when any membership is unknown to the organization, has no V2 upgrade token, or has a different
    /// user key id than the one given. Nothing is written in that case.
    /// </exception>
    Task ApplyAsync(Guid organizationId, IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates);
}
