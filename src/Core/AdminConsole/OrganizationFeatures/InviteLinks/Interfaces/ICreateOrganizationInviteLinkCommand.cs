using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface ICreateOrganizationInviteLinkCommand
{
    /// <summary>
    /// Creates a new invite link for the specified organization.
    /// </summary>
    /// <param name="organizationId">The organization to create the invite link for.</param>
    /// <param name="allowedDomains">Email domains that are permitted to accept the invite. At least one is required.</param>
    /// <param name="encryptedInviteKey">The invite key wrapped with the organization key. Never contains the raw key.</param>
    /// <returns>The created <see cref="OrganizationInviteLink"/>, or an error if validation fails or a link already exists.</returns>
    Task<CommandResult<OrganizationInviteLink>> CreateAsync(
        Guid organizationId,
        IEnumerable<string> allowedDomains,
        string encryptedInviteKey);
}
