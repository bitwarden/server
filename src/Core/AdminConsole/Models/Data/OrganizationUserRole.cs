using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data;

/// <summary>
/// A lightweight <see cref="IOrganizationUserRole"/> for when a full OrganizationUser isn't available.
/// </summary>
public sealed record OrganizationUserRole(
    OrganizationUserType Type,
    Guid OrganizationId,
    Permissions? Permissions = null) : IOrganizationUserRole
{
    public Permissions? GetPermissions() => Permissions;
}
