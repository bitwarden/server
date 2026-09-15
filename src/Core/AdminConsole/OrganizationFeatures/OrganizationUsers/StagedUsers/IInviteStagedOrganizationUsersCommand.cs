using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

/// <summary>
/// Invites organization users who were previously provisioned as <see cref="OrganizationUserStatusType.Staged"/>.
/// </summary>
public interface IInviteStagedOrganizationUsersCommand
{
    /// <summary>
    /// Moves staged members to <see cref="OrganizationUserStatusType.Invited"/> and emails them an invitation,
    /// leaving their role, collection access, and group membership as provisioned.
    /// </summary>
    /// <remarks>
    /// Members that are missing, belong to another organization, or are no longer staged are skipped and
    /// reported individually rather than failing the request. Seat expansion remains all-or-nothing because
    /// seats are reserved once for the whole eligible set. Secrets Manager access is carried over as
    /// provisioned, except that an organization holding the Secrets Manager standalone discount grants it to
    /// everyone, matching the invite, invite-link, and import paths.
    /// </remarks>
    /// <param name="request">The organization, the staged members to invite, and the inviting administrator.</param>
    /// <returns>
    /// A per-member result for every requested id, or an error if the organization is missing or no seats
    /// could be reserved.
    /// </returns>
    Task<CommandResult<ICollection<BulkCommandResult>>> RunAsync(InviteStagedOrganizationUsersRequest request);
}
