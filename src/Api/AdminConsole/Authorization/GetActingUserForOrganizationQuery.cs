using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;

namespace Bit.Api.AdminConsole.Authorization;

public class GetActingUserForOrganizationQuery(ICurrentContext currentContext) : IGetActingUserForOrganizationQuery
{
    public async Task<IActingUser> GetActingUserAsync(Guid userId, Guid organizationId)
    {
        var membership = currentContext.GetOrganization(organizationId);
        if (membership is not null)
        {
            return new StandardUser(userId, membership.Type == OrganizationUserType.Owner, membership.Type, membership.Permissions);
        }

        var providerId = await currentContext.ProviderIdForOrg(organizationId);
        if (providerId is not null)
        {
            var providerUserType = currentContext.ProviderProviderAdmin(providerId.Value)
                ? ProviderUserType.ProviderAdmin
                : ProviderUserType.ServiceUser;
            return new ProviderUser(userId, providerId.Value, providerUserType);
        }

        throw new NotFoundException();
    }
}
