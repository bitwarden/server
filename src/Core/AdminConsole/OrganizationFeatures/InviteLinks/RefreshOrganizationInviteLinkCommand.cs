using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class RefreshOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider)
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
            Invite = request.Invite,
            // Auto confirmation isn't supported yet — force to false regardless of the request.
            SupportsConfirmation = false,
            CreationDate = now,
            RevisionDate = now,
        };
        newLink.SetNewId();

        await organizationInviteLinkRepository.RefreshAsync(existing, newLink);

        return newLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
