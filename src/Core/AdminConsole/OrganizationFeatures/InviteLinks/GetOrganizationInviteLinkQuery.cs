using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class GetOrganizationInviteLinkQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IApplicationCacheService applicationCacheService)
    : IGetOrganizationInviteLinkQuery
{
    public async Task<CommandResult<OrganizationInviteLink>> GetAsync(Guid organizationId)
    {
        if (!await OrganizationHasInviteLinksAbilityAsync(organizationId))
        {
            return new InviteLinkNotAvailable();
        }

        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(organizationId);
        if (link is null)
        {
            return new InviteLinkNotFound();
        }

        return link;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }
}
