using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

public interface IActingUser
{
    Guid? UserId { get; }
    bool IsOrganizationOwnerOrProvider { get; }
    EventSystemUser? SystemUserType { get; }
    Permissions? Permissions { get; }
    OrganizationUserType? OrganizationUserType { get; }
}
