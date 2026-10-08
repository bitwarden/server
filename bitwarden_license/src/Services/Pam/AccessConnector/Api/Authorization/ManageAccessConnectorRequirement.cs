using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.AccessConnector.Api.Authorization;

/// <summary>
/// Requires an Owner, an Admin, or a Custom user holding <see cref="Permissions.ManageRotation"/>. Providers are
/// excluded, since an access connector holds the organization key and rotation rewrites vault credentials.
/// </summary>
public class ManageAccessConnectorRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    {
        var authorized = organizationClaims is
        { Type: OrganizationUserType.Owner }
            or { Type: OrganizationUserType.Admin }
            or { Type: OrganizationUserType.Custom, Permissions.ManageRotation: true };

        return Task.FromResult(authorized);
    }
}
