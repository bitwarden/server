using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public interface IBulkResendOrganizationInvitesCommand
{
    /// <summary>
    /// Resend invites to multiple organization users in bulk.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="invitingUserId">The ID of the user who is resending the invites.</param>
    /// <param name="organizationUsersId">The IDs of the organization users to resend invites to.</param>
    /// <returns>A tuple containing the OrganizationUser and an error message (empty string if successful)</returns>
    Task<IEnumerable<Tuple<OrganizationUser, string>>> BulkResendInvitesAsync(
        Guid organizationId,
        Guid? invitingUserId,
        IEnumerable<Guid> organizationUsersId);
}


