using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Retrieves the opaque invite blob for an invite link. See
/// <see cref="IGetOrganizationInviteBlobCommand"/> for the behavior.
/// </summary>
public class GetOrganizationInviteBlobCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService)
    : IGetOrganizationInviteBlobCommand
{
    public async Task<CommandResult<string>> GetInviteBlobAsync(GetOrganizationInviteBlobRequest request)
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
