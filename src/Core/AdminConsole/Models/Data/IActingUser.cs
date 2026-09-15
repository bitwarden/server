using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public interface IActingUser
{
    Guid? UserId { get; }
    [Obsolete("This property is obsolete. Use isProvider or the OrganizationUserType where available instead.")]
    bool IsOrganizationOwnerOrProvider { get; }
    EventSystemUser? SystemUserType { get; }
}
