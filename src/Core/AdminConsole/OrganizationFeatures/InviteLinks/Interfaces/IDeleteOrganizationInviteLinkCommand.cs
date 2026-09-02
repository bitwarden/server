using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IDeleteOrganizationInviteLinkCommand
{
    /// <summary>
    /// Deletes the invite link for the specified organization.
    /// </summary>
    /// <param name="organizationId">The ID of the organization whose invite link should be deleted.</param>
    /// <returns>A successful result, or <see cref="InviteLinkNotFound"/> if the organization has no invite link.</returns>
    Task<CommandResult> DeleteAsync(Guid organizationId);
}
