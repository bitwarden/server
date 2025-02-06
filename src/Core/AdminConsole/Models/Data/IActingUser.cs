using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

public interface IActingUser
{
    Guid? UserId { get; }
    bool IsOrganizationOwnerOrProvider { get; }
    EventSystemUser? SystemUserType { get; }
}
