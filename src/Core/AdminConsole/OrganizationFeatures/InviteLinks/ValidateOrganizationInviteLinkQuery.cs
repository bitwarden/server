using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class ValidateOrganizationInviteLinkQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository)
    : IValidateOrganizationInviteLinkQuery
{
    public async Task<CommandResult> ValidateAsync(Guid organizationId, Guid code, string email)
    {
        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(organizationId);
        if (inviteLink is null || !inviteLink.CodeMatches(code.ToString()))
        {
            return new InviteLinkNotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(inviteLink.OrganizationId);
        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        // Gate on the same check every other invite-link consumer uses. Possession of a valid
        // {orgId, code} is not sufficient — the link must actually admit this email domain.
        // The caller applies a domain-block-policy exclusion on success, so an insufficient
        // check here would let a bearer of the code bypass a claimed-domain block by
        // registering an email the link would reject at accept time.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(email, inviteLink.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed(organization.DisplayName());
        }

        return new None();
    }
}
