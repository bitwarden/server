using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public class SystemUser : IActingUser
{
    public SystemUser(EventSystemUser systemUser)
    {
        SystemUserType = systemUser;
    }

    public Guid? UserId => throw new Exception($"{nameof(SystemUserType)} does not have a {nameof(UserId)}.");

    public bool IsOrganizationOwnerOrProvider => false;
    public EventSystemUser? SystemUserType { get; }
}
