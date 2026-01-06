#nullable enable

using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Represents an entity that has organization user permissions stored as a JSON string.
/// </summary>
public interface IOrganizationUserPermissions
{
    /// <summary>
    /// A json blob representing the <see cref="Bit.Core.Models.Data.Permissions"/> of the OrganizationUser if they
    /// are a Custom user role. MAY be NULL if they are not a custom user, but this is not guaranteed;
    /// do not use this to determine their role.
    /// </summary>
    /// <remarks>
    /// Avoid using this property directly - instead use the extension methods
    /// <see cref="OrganizationUserPermissionsExtensions.GetPermissions"/> and
    /// <see cref="OrganizationUserPermissionsExtensions.SetPermissions"/>.
    /// </remarks>
    string? Permissions { get; set; }
}

/// <summary>
/// Extension methods for working with <see cref="IOrganizationUserPermissions"/> implementations.
/// </summary>
public static class OrganizationUserPermissionsExtensions
{
    /// <summary>
    /// Deserializes the Permissions JSON string into a <see cref="Permissions"/> object.
    /// </summary>
    /// <param name="organizationUser">The organization user with permissions.</param>
    /// <returns>A <see cref="Permissions"/> object if the JSON is valid, otherwise null.</returns>
    public static Permissions? GetPermissions(this IOrganizationUserPermissions organizationUser)
    {
        return string.IsNullOrWhiteSpace(organizationUser.Permissions) ? null
            : CoreHelpers.LoadClassFromJsonData<Permissions>(organizationUser.Permissions);
    }

    /// <summary>
    /// Serializes a <see cref="Permissions"/> object into a JSON string and stores it.
    /// </summary>
    /// <param name="organizationUser">The organization user to set permissions on.</param>
    /// <param name="permissions">The permissions object to serialize.</param>
    public static void SetPermissions(this IOrganizationUserPermissions organizationUser, Permissions permissions)
    {
        organizationUser.Permissions = CoreHelpers.ClassToJsonData(permissions);
    }
}
