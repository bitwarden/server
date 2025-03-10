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
    public static AccessClientType ToAccessClient(IdentityClientType identityClientType, bool bypassAccessCheck = false)
    {
        if (bypassAccessCheck)
        {
            return AccessClientType.NoAccessCheck;
        }

        return identityClientType switch
        {
            IdentityClientType.User => AccessClientType.User,
            IdentityClientType.Organization => AccessClientType.Organization,
            IdentityClientType.ServiceAccount => AccessClientType.ServiceAccount,
            _ => throw new ArgumentOutOfRangeException(nameof(identityClientType), identityClientType, null),
        };
    }
}
