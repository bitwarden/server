namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public interface IResendOrganizationInviteCommand
{
    /// <summary>
    /// Resend an invite to an organization user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="invitingUserId">The ID of the user who is inviting the organization user.</param>
    /// <param name="organizationUserId">The ID of the organization user to resend the invite to.</param>
    /// <param name="initOrganization">Whether to initialize the organization. 
    /// This is should only be true when inviting the owner of a new organization.</param>
    Task ResendInviteAsync(Guid organizationId, Guid? invitingUserId, Guid organizationUserId, bool initOrganization = false);
}
