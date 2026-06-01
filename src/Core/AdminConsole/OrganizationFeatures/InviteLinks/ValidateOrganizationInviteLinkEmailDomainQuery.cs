using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class ValidateOrganizationInviteLinkEmailDomainQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository)
    : IValidateOrganizationInviteLinkEmailDomainQuery
{
    public async Task<CommandResult<bool>> ValidateAsync(Guid code, string email)
    {
        var link = await organizationInviteLinkRepository.GetByCodeAsync(code);
        if (link is null)
        {
            return new InviteLinkNotFound();
        }

        return InviteLinkDomainValidator.IsEmailDomainAllowed(email, link.GetAllowedDomains());
    }
}
