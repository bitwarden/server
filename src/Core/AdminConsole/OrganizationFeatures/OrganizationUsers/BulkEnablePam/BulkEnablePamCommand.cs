using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Repositories;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;

public class BulkEnablePamCommand(
    IOrganizationUserRepository organizationUserRepository,
    IReservePrivilegedControlsSeatsCommand reservePrivilegedControlsSeatsCommand) : IBulkEnablePamCommand
{
    public async Task<CommandResult> BulkEnablePamAsync(Organization organization, IEnumerable<Guid> organizationUserIds)
    {
        var organizationUsers = (await organizationUserRepository.GetManyAsync(organizationUserIds))
            .Where(ou => ou.OrganizationId == organization.Id && !ou.AccessPam)
            .ToList();
        if (organizationUsers.Count == 0)
        {
            return new UsersInvalid();
        }

        // Granting access on an organization without PAM would be inert: claim emission ANDs AccessPam with the
        // organization's UsePam.
        if (!organization.UsePam)
        {
            return new PamNotEnabled();
        }

        var membersTakingSeats = organizationUsers.Count(u => PrivilegedControlsSeatOccupancy.OccupiesSeat(u.Status));
        var reservation = await reservePrivilegedControlsSeatsCommand.RunAsync(organization, membersTakingSeats);
        if (!reservation.Success)
        {
            return reservation.Error!;
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.AccessPam = true;
        }

        await organizationUserRepository.ReplaceManyAsync(organizationUsers);
        return new None();
    }
}
