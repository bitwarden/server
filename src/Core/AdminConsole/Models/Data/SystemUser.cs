using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public class SystemUser(EventSystemUser systemUser) : IActingUser
{
    public Guid? UserId => throw new Exception($"{nameof(SystemUserType)} does not have a {nameof(UserId)}.");
    [Obsolete("This property is obsolete.")]
    public bool IsOrganizationOwnerOrProvider => false;
    public EventSystemUser? SystemUserType { get; } = systemUser;
    public bool IsProvider => false;
}
