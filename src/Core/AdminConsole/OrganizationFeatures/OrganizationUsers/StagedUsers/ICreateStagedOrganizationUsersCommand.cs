using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

/// <summary>
/// Provisions organization users in <see cref="Bit.Core.Enums.OrganizationUserStatusType.Staged"/> status.
/// </summary>
public interface ICreateStagedOrganizationUsersCommand
{
    /// <summary>
    /// Creates a Staged <see cref="OrganizationUser"/> for each user in the request whose email does not
    /// already belong to the organization. Unlike the invite flow, this performs no seat-count validation,
    /// no seat autoscale, and sends no invitation email.
    /// </summary>
    /// <param name="request">The organization, the users to stage, and the system performing the provisioning.</param>
    /// <returns>
    /// A <see cref="CommandResult{T}"/> wrapping the created Staged organization users. The collection is
    /// empty if every email was already present.
    /// </returns>
    Task<CommandResult<ICollection<OrganizationUser>>> RunAsync(CreateStagedOrganizationUsersRequest request);
}
