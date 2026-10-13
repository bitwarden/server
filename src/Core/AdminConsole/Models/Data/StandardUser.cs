using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

public class StandardUser(Guid userId, bool isProvider, OrganizationUserType? orgUserType = null, Permissions? permissions = null) : IActingUser
{
    public Guid? UserId { get; } = userId;
    [Obsolete("This property is obsolete. Use isProvider or the OrganizationUserType.")]
    public bool IsOrganizationOwnerOrProvider => OrganizationUserType is Core.Enums.OrganizationUserType.Owner || IsProvider;
    public OrganizationUserType? OrganizationUserType { get; } = orgUserType;
    public Permissions? Permissions { get; } = permissions;
    public EventSystemUser? SystemUserType => throw new Exception($"{nameof(StandardUser)} does not have a {nameof(SystemUserType)}");
    public bool IsProvider { get; } = isProvider;
}
