using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class UpdateOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider,
    IOrganizationRepository organizationRepository,
    IEventService eventService)
    : IUpdateOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationInviteLink>> UpdateAsync(
        UpdateOrganizationInviteLinkRequest request)
    {
        if (!await OrganizationHasInviteLinksAbilityAsync(request.OrganizationId))
        {
            return new InviteLinkNotAvailable();
        }

        var sanitizedDomains = InviteLinkDomainSanitizer.SanitizeDomains(request.AllowedDomains);
        if (sanitizedDomains.Count == 0)
        {
            return new InviteLinkDomainsRequired();
        }

        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (inviteLink is null)
        {
            return new InviteLinkNotFound();
        }

        inviteLink.SetAllowedDomains(sanitizedDomains);
        inviteLink.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;

        await organizationInviteLinkRepository.ReplaceAsync(inviteLink);

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is not null)
        {
            await eventService.LogOrganizationEventAsync(organization, EventType.Organization_InviteLinkDomainsEdited);
        }

        return inviteLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
