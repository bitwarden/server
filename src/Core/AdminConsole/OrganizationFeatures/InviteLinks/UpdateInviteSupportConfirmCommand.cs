using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class UpdateInviteSupportConfirmCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider,
    IEventService eventService)
    : IUpdateInviteSupportConfirmCommand
{
    public async Task<CommandResult<OrganizationInviteLink>> UpdateAsync(
        UpdateInviteSupportConfirmRequest request)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(request.OrganizationId);
        if (ability is null || !ability.UseInviteLinks)
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

        await LogConfirmationSupportEventAsync(request, ability);

        return inviteLink;
    }

    /// <summary>
    /// Records whether the admin turned automatic confirmation on or off.
    /// </summary>
    private async Task LogConfirmationSupportEventAsync(
        UpdateInviteSupportConfirmRequest request, OrganizationAbility ability)
    {
        var eventType = request.SupportsConfirmation
            ? EventType.Organization_InviteLinkConfirmEnabled
            : EventType.Organization_InviteLinkConfirmDisabled;

        await eventService.LogOrganizationEventAsync(ability, eventType);
    }
}
