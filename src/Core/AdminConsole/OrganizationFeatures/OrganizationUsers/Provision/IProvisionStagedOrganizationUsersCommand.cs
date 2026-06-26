using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;

/// <summary>
/// Provisions organization users in <see cref="Bit.Core.Enums.OrganizationUserStatusType.Staged"/> status.
/// Staged users are placeholders created by automated directory sync (SCIM / Directory Connector): they do not
/// yet consume a seat, are not subject to organization policies, and have not been sent an invitation.
/// </summary>
public interface IProvisionStagedOrganizationUsersCommand
{
    /// <summary>
    /// Creates a Staged <see cref="OrganizationUser"/> for each (email, externalId) pair whose email does not
    /// already belong to the organization. Unlike the invite flow, this performs no seat-count validation,
    /// no seat autoscale, and sends no invitation email.
    /// </summary>
    /// <param name="organization">The organization to provision the staged users into.</param>
    /// <param name="users">
    /// The (email, externalId) pairs to stage. Emails already present in the organization, and duplicate
    /// emails within the batch, are skipped.
    /// </param>
    /// <param name="eventSystemUser">The automated system performing the provisioning, used for event attribution.</param>
    /// <returns>The created Staged organization users. Empty if every email was already present.</returns>
    Task<ICollection<OrganizationUser>> ProvisionStagedOrganizationUsersAsync(
        Organization organization,
        IEnumerable<(string Email, string ExternalId)> users,
        EventSystemUser eventSystemUser);
}
