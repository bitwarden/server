using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Authorization;

public class GetActingUserForOrganizationQuery(ICurrentContext currentContext) : IGetActingUserForOrganizationQuery
{
    public async Task<IActingUser> GetActingUserAsync(Guid userId, Guid organizationId)
    {
        var membership = currentContext.GetOrganization(organizationId);
        var isProvider = await currentContext.ProviderUserForOrgAsync(organizationId);

        return new StandardUser(userId, isProvider, membership?.Type, membership?.Permissions);
    }
}
