using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Retrieves the opaque invite for an invite link. See
/// <see cref="IGetOrganizationInviteCommand"/> for the behavior.
/// </summary>
public class GetOrganizationInviteCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService)
    : IGetOrganizationInviteCommand
{
    public async Task<CommandResult<string>> GetInviteAsync(GetOrganizationInviteRequest request)
    {
        var user = request.User;

        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (link is null || !link.CodeMatches(request.Code.ToString()))
        {
            return new InviteLinkNotFound();
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(link.OrganizationId);
        if (organizationAbility is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organizationAbility.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed();
        }

        return link.Invite;
    }
}
