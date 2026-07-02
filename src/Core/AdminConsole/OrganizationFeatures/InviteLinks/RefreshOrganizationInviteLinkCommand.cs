using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class RefreshOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider,
    IOrganizationRepository organizationRepository,
    IEventService eventService)
    : IRefreshOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationInviteLink>> RefreshAsync(
        RefreshOrganizationInviteLinkRequest request)
    {
        if (!await OrganizationHasInviteLinksAbilityAsync(request.OrganizationId))
        {
            return new InviteLinkNotAvailable();
        }

        var existing = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (existing is null)
        {
            return new InviteLinkNotFound();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newLink = new OrganizationInviteLink
        {
            OrganizationId = existing.OrganizationId,
            AllowedDomains = existing.AllowedDomains,
            EncryptedInviteKey = request.EncryptedInviteKey,
            EncryptedOrgKey = request.EncryptedOrgKey,
            CreationDate = now,
            RevisionDate = now,
        };
        newLink.SetNewId();

        await organizationInviteLinkRepository.RefreshAsync(existing, newLink);

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is not null)
        {
            await eventService.LogOrganizationEventAsync(organization, EventType.Organization_InviteLinkRefreshed);
        }

        return newLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
