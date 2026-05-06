using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class RefreshOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IApplicationCacheService applicationCacheService,
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
            EncryptedInviteKey = request.EncryptedInviteKey,
            EncryptedOrgKey = request.EncryptedOrgKey,
            CreationDate = now,
            RevisionDate = now,
        };
        newLink.SetNewId();

        await organizationInviteLinkRepository.RefreshAsync(existing, newLink);

        return newLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
