using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

public class StandardUser(Guid userId, bool isOrganizationOwner, OrganizationUserType? orgUserType = null,
    Permissions? permissions = null) : IActingUser
{
    public Guid? UserId { get; } = userId;
    public bool IsOrganizationOwnerOrProvider { get; } = isOrganizationOwner;
    public OrganizationUserType? OrganizationUserType { get; } = orgUserType;
    public Permissions? Permissions { get; } = permissions;
    public EventSystemUser? SystemUserType => throw new Exception($"{nameof(StandardUser)} does not have a {nameof(SystemUserType)}");

}
