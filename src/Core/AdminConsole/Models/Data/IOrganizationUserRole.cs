using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

/// <summary>
/// The minimal role information needed to authorize whether one member can manage another.
/// </summary>
public interface IOrganizationUserRole
{
    OrganizationUserType Type { get; }
    Guid OrganizationId { get; }
    Permissions? GetPermissions();
}
