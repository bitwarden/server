using Bit.Core.Context;
using Bit.Core.Identity;

namespace Bit.Core.Enums;

public enum AccessClientType
{
    Admin = -1,
    User = 0,
    Organization = 1,
    ServiceAccount = 2,
}

public static class AccessClientHelper
{
    public static async Task<AccessClientType> ToAccessClient(ICurrentContext currentContext, Guid organizationId)
    {
        if (await currentContext.OrganizationAdmin(organizationId))
        {
            return AccessClientType.Admin;
        }

        return currentContext.ClientType switch
        {
            ClientType.User => AccessClientType.User,
            ClientType.Organization => AccessClientType.Organization,
            ClientType.ServiceAccount => AccessClientType.ServiceAccount,
        };
    }
}
