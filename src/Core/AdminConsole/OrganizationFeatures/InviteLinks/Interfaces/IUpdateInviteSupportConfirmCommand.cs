using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IUpdateInviteSupportConfirmCommand
{
    /// <summary>
    /// Updates only the <see cref="OrganizationInviteLink.Invite"/> blob and
    /// <see cref="OrganizationInviteLink.SupportsConfirmation"/> flag for the specified organization's invite link.
    /// </summary>
    /// <param name="request">The details for the invite link update.</param>
    /// <returns>The updated <see cref="OrganizationInviteLink"/>, or an error if the organization does not support
    /// invite links or a link does not exist.</returns>
    Task<CommandResult<OrganizationInviteLink>> UpdateAsync(UpdateInviteSupportConfirmRequest request);
}
