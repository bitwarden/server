using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;

public class BulkEnablePamCommand(
    IOrganizationUserRepository organizationUserRepository,
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IGlobalSettings globalSettings) : IBulkEnablePamCommand
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

        var additionalPamSeatsRequired =
            await countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, organizationUsers.Count);
        if (additionalPamSeatsRequired > 0 && globalSettings.SelfHosted)
        {
            return new CannotAutoscalePamSeatsOnSelfHost();
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.AccessPam = true;
        }

        await organizationUserRepository.ReplaceManyAsync(organizationUsers);
        return new None();
    }
}
