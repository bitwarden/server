using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.AccessConnector.Api.Authorization;

/// <summary>
/// Requires authority over an organization's rotation fleet and configuration: an Owner or an Admin.
/// </summary>
/// <remarks>
/// There is no custom-permission arm. The only PAM permission is <see cref="Permissions.ManageAccessRules"/>, which is
/// authority over rule authorship — over who may lease a credential — not over the access connectors that rewrite those
/// credentials at the target system.
/// <para>
/// This implements <see cref="IOrganizationRequirement"/> directly rather than deriving from
/// <c>BasePermissionRequirement</c>, whose final arm authorizes any provider managing the organization. Registering an
/// access connector hands it the organization key and rotation rewrites the credentials inside the vault, neither of
/// which is a provider's to hold or change. A non-member has no organization claims and so is never authorized.
/// </para>
/// </remarks>
public class ManageAccessConnectorRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    {
        var authorized = organizationClaims is
        { Type: OrganizationUserType.Owner } or { Type: OrganizationUserType.Admin };

        return Task.FromResult(authorized);
    }
}
