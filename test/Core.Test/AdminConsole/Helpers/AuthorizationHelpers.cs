using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Test.AdminConsole.Helpers;

public static class AuthorizationHelpers
{
    /// <summary>
    /// Return a new Permission object with inverted permissions.
    /// This is useful to test negative cases, e.g. "all other permissions should fail".
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static Permissions Invert(this Permissions permissions)
    {
        // Get all false boolean properties of input object
        var inputsToFlip = permissions
            .GetType()
            .GetProperties()
            .Where(p =>
                p.PropertyType == typeof(bool) &&
                (bool)p.GetValue(permissions, null)! == false)
            .Select(p => p.Name);

        var result = new Permissions();

        // Set these to true on the result object
        result
            .GetType()
            .GetProperties()
            .Where(p => inputsToFlip.Contains(p.Name))
            .ToList()
            .ForEach(p => p.SetValue(result, true));

        return result;
    }

    /// <summary>
    /// Returns a sequence of all possible roles and permissions represented as CurrentContextOrganization objects.
    /// Used largely for authorization testing.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<CurrentContextOrganization> AllRoles() => new List<CurrentContextOrganization>
    {
        new () { Type = OrganizationUserType.Owner },
        new () { Type = OrganizationUserType.Admin },
        new () { Type = OrganizationUserType.Custom, Permissions = new Permissions() },
        new () { Type = OrganizationUserType.Custom, Permissions = new Permissions().Invert() },
        new () { Type = OrganizationUserType.User },
    };
}
