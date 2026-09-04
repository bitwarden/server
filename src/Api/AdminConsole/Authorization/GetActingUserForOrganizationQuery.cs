using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization;

public class GetActingUserForOrganizationQuery(
    ICurrentContext currentContext,
    IProviderUserRepository providerUserRepository) : IGetActingUserForOrganizationQuery
{
    public async Task<IActingUser> GetActingUserAsync(Guid userId, Guid organizationId)
    {
        var membership = currentContext.GetOrganization(organizationId);
        if (membership is not null)
        {
            return new StandardUser(userId, membership.Type == OrganizationUserType.Owner,
                membership.Type, membership.Permissions);
        }

        // A provider's link to an organization isn't carried in the context, so it must be resolved from the database.
        var providerMembership = (await providerUserRepository
                .GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed))
            .FirstOrDefault(po => po.OrganizationId == organizationId);
        if (providerMembership is not null)
        {
            return new ProviderUser(userId, providerMembership.ProviderId!.Value, providerMembership.Type);
        }

        return new StandardUser(userId, isOrganizationOwner: false);
    }
}
