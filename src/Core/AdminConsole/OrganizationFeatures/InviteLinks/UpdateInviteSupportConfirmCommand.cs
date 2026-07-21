using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class UpdateInviteSupportConfirmCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider)
    : IUpdateInviteSupportConfirmCommand
{
    public async Task<CommandResult<OrganizationInviteLink>> UpdateAsync(
        UpdateInviteSupportConfirmRequest request)
    {
        if (!await OrganizationHasInviteLinksAbilityAsync(request.OrganizationId))
        {
            return new InviteLinkNotAvailable();
        }

        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (inviteLink is null)
        {
            return new InviteLinkNotFound();
        }

        inviteLink.Invite = request.Invite;
        inviteLink.SupportsConfirmation = request.SupportsConfirmation;
        inviteLink.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;

        await organizationInviteLinkRepository.ReplaceAsync(inviteLink);

        return inviteLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
