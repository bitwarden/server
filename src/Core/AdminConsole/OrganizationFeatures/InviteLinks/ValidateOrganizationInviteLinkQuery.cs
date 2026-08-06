using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class ValidateOrganizationInviteLinkQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository)
    : IValidateOrganizationInviteLinkQuery
{
    public async Task<CommandResult> ValidateAsync(Guid organizationId, Guid code)
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

        return new None();
    }
}
