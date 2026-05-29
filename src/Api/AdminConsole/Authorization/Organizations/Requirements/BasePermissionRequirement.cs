using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// A base implementation of <see cref="IOrganizationRequirement"/> which will authorize Owners, Admins, Providers,
/// and custom users with the permission specified by the permissionPicker constructor parameter. This is suitable
/// for most requirements related to a custom permission.
/// </summary>
/// <param name="permissionPicker">A function that returns a custom permission which will authorize the action.</param>
public abstract class BasePermissionRequirement(Func<Permissions, bool> permissionPicker) : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    => organizationClaims switch
    {
        { Type: OrganizationUserType.Owner } => true,
        { Type: OrganizationUserType.Admin } => true,
        { Type: OrganizationUserType.Custom } when permissionPicker(organizationClaims.Permissions) => true,
        _ => await isProviderUserForOrg()
    };
}
