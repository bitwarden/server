using Bit.Core.Identity;

namespace Bit.Core.Enums;

public enum AccessClientType
{
    NoAccessCheck = -1,
    User = 0,
    Organization = 1,
    ServiceAccount = 2,
}

public static class AccessClientHelper
{
    public static AccessClientType ToAccessClient(ClientType clientType, bool bypassAccessCheck = false)
    {
        if (bypassAccessCheck)
        {
            return AccessClientType.NoAccessCheck;
        }

        return clientType switch
        {
            ClientType.User => AccessClientType.User,
            ClientType.Organization => AccessClientType.Organization,
        };
    }
}
