using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IAcceptOrganizationInviteLinkCommand
{
    /// <summary>
    /// Accepts an organization invite link for the given user, joining them to the organization.
    /// </summary>
    /// <param name="request">The invite link code and the accepting user (and reset password key, when required).</param>
    /// <returns>The accepted <see cref="OrganizationUser"/>, or a coded <see cref="InviteLinkValidationError"/> if the link is invalid or the user cannot join.</returns>
    Task<Result<OrganizationUser, InviteLinkValidationError>> AcceptAsync(AcceptOrganizationInviteLinkRequest request);
}
