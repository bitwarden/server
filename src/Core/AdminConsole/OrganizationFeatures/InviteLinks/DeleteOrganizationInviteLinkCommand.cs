using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class DeleteOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IEventService eventService)
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

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is not null)
        {
            await eventService.LogOrganizationEventAsync(organization, EventType.Organization_InviteLinkDeleted);
        }

        return new None();
    }
}
