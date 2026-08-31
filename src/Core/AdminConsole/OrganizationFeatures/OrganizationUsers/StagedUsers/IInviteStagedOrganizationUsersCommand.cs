using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

/// <summary>
/// Invites organization users who were previously provisioned as <see cref="OrganizationUserStatusType.Staged"/>.
/// </summary>
public interface IInviteStagedOrganizationUsersCommand
{
    /// <summary>
    /// Moves staged members to <see cref="OrganizationUserStatusType.Invited"/> and emails them an invitation,
    /// leaving their role, collection access, group membership, and Secrets Manager access as provisioned.
    /// </summary>
    /// <remarks>
    /// The batch is all-or-nothing: if any member is missing, belongs to another organization, or is not
    /// staged, nothing is changed.
    /// </remarks>
    /// <param name="request">The organization, the staged members to invite, and the inviting administrator.</param>
    /// <returns>
    /// The invited <see cref="OrganizationUser"/> rows, or an error if validation fails or no seats could be reserved.
    /// </returns>
    Task<CommandResult<ICollection<OrganizationUser>>> RunAsync(InviteStagedOrganizationUsersRequest request);
}
