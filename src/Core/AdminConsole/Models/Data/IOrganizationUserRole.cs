using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

/// <summary>
/// Minimal role information for a user.
/// </summary>
public interface IOrganizationUserRole
{
    OrganizationUserType Type { get; }
    Guid OrganizationId { get; }
    Permissions? GetPermissions();
}
