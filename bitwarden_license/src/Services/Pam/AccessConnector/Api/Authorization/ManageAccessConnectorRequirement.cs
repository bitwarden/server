using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.AccessConnector.Api.Authorization;

/// <summary>
/// Requires authority over an organization's rotation fleet and configuration: an Owner, an Admin, or a Custom user
/// holding <see cref="Permissions.ManageRotation"/>.
/// </summary>
/// <remarks>
/// This implements <see cref="IOrganizationRequirement"/> directly rather than deriving from
/// <c>BasePermissionRequirement</c>, whose final arm authorizes any provider managing the organization. Registering an
/// access connector hands it the organization key and rotation rewrites the credentials inside the vault, neither of
/// which is a provider's to hold or change. A non-member has no organization claims and so is never authorized.
/// </remarks>
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
