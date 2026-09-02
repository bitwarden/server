using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface ICreateOrganizationInviteLinkCommand
{
    /// <summary>
    /// Creates a new invite link for the specified organization.
    /// </summary>
    /// <param name="request">The details for the invite link to create.</param>
    /// <returns>The created <see cref="OrganizationInviteLink"/>, or an error if validation fails or a link already exists.</returns>
    Task<CommandResult<OrganizationInviteLink>> CreateAsync(CreateOrganizationInviteLinkRequest request);
}
