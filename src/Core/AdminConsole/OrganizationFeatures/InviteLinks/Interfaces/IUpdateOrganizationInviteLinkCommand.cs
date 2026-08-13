using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IUpdateOrganizationInviteLinkCommand
{
    /// <summary>
    /// Updates the allowed domains for the invite link of the specified organization.
    /// </summary>
    /// <param name="request">The details for the invite link update.</param>
    /// <returns>The updated <see cref="OrganizationInviteLink"/>, or an error if validation fails or a link does not exist.</returns>
    Task<CommandResult<OrganizationInviteLink>> UpdateAsync(UpdateOrganizationInviteLinkRequest request);
}
