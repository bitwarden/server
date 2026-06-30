using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class CreateOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    TimeProvider timeProvider)
    : ICreateOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationInviteLink>> CreateAsync(
        CreateOrganizationInviteLinkRequest request)
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

        var existingLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (existingLink != null)
        {
            return new InviteLinkAlreadyExists();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = request.OrganizationId,
            Invite = request.Invite,
            // Confirmation isn't supported until Milestone 3; links can only be used to accept for now.
            SupportsConfirmation = false,
            CreationDate = now,
            RevisionDate = now,
        };
        inviteLink.SetAllowedDomains(sanitizedDomains);
        inviteLink.SetNewId();

        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return inviteLink;
    }

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }

}
