using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class SetOrganizationPartnershipStatusCommand(
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    TimeProvider timeProvider)
    : ISetOrganizationPartnershipStatusCommand
{
    public async Task<CommandResult<OrganizationPartnership>> SetStatusAsync(Guid organizationId, PartnershipStatus status)
    {
        var partnership = await organizationPartnershipRepository.GetByOrganizationIdAsync(organizationId);
        if (partnership is null)
        {
            return new PartnershipNotFound();
        }

        if (partnership.Status == status)
        {
            return partnership;
        }

        partnership.Status = status;
        partnership.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        await organizationPartnershipRepository.ReplaceAsync(partnership);

        return partnership;
    }
}
