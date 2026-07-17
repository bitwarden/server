using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

public class SystemUser(EventSystemUser systemUser) : IActingUser
{
    public Guid? UserId => throw new Exception($"{nameof(SystemUserType)} does not have a {nameof(UserId)}.");

    public bool IsOrganizationOwnerOrProvider => false;
    public EventSystemUser? SystemUserType { get; } = systemUser;
    public Permissions? Permissions => null;
    public OrganizationUserType? OrganizationUserType => null;
}
