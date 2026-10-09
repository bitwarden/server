using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Retrieves the opaque invite for an invite link. See
/// <see cref="IGetOrganizationInviteCommand"/> for the behavior.
/// </summary>
/// <remarks>
/// This command looks up the invite link, organization, and existing membership, and delegates eligibility to
/// <see cref="IGetOrganizationInviteValidator"/>.
/// </remarks>
public class GetOrganizationInviteCommand(
    IGetOrganizationInviteValidator getOrganizationInviteValidator,
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository)
    : IGetOrganizationInviteCommand
{
    public async Task<CommandResult<string>> GetInviteAsync(GetOrganizationInviteRequest request)
    {
        var validationResult = await getOrganizationInviteValidator.ValidateAsync(await BuildValidationRequestAsync(request));
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        // Validation guarantees the invite link exists.
        return validationResult.Request.InviteLink!.Invite;
    }

    /// <summary>
    /// Looks up the invite link, its organization, and the user's existing membership for the validator.
    /// </summary>
    private async Task<OrganizationInviteLinkValidationRequest> BuildValidationRequestAsync(
        GetOrganizationInviteRequest request)
    {
        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        var organization = inviteLink is null
            ? null
            : await organizationRepository.GetByIdAsync(request.OrganizationId);
        var existingOrganizationUser = organization is null
            ? null
            : await ResolveExistingOrganizationUserAsync(organization, request.User);

        return new OrganizationInviteLinkValidationRequest
        {
            InviteLink = inviteLink,
            Code = request.Code,
            Organization = organization,
            User = request.User,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }

    /// <summary>
    /// Resolves the user's existing membership, preferring a user-linked membership and falling back to a
    /// pending email invitation for the same address.
    /// </summary>
    private async Task<OrganizationUser?> ResolveExistingOrganizationUserAsync(Organization organization, User user)
    {
        var userLinkedOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        if (userLinkedOrganizationUser is not null)
        {
            return userLinkedOrganizationUser;
        }

        return await organizationUserRepository.GetByOrganizationEmailAsync(organization.Id, user.Email);
    }
}
