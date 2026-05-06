using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class DeleteOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository)
    : IDeleteOrganizationInviteLinkCommand
{
    public async Task<CommandResult> DeleteAsync(Guid organizationId)
    {
        var existing = await organizationInviteLinkRepository.GetByOrganizationIdAsync(organizationId);
        if (existing is null)
        {
            return new InviteLinkNotFound();
        }

        await organizationInviteLinkRepository.DeleteAsync(existing);
        return new None();
    }
}
