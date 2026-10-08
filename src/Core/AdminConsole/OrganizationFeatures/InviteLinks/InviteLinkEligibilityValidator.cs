using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkEligibilityValidator"/>.
/// </summary>
public class InviteLinkEligibilityValidator(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IOrganizationRepository organizationRepository)
    : IInviteLinkEligibilityValidator
{
    public async Task<CommandResult<OrganizationInviteLink>> ValidateAsync(InviteLinkEligibilityValidationRequest request)
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

        // The domain is checked before email verification so a user on a disallowed domain is told so first,
        // rather than being sent through the client's email verification step only to be rejected afterwards.
        // Auth's invite link email verification flow (PM-42435) relies on this order; coordinate with Auth before
        // changing it.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            // The organization name is only needed for this error, so it is not read on the happy path.
            var organization = await organizationRepository.GetByIdAsync(link.OrganizationId);
            return new EmailDomainNotAllowed(organization?.DisplayName() ?? string.Empty);
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        if (!user.EmailVerified)
        {
            return new EmailNotVerified();
        }

        return link;
    }
}
