using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.Api.Authorization;

/// <summary>
/// Requires authority over access-rule authorship in the organization: an Owner, an Admin, or a Custom user holding
/// <see cref="Permissions.ManageAccessRules"/>.
/// </summary>
/// <remarks>
/// This implements <see cref="IOrganizationRequirement"/> directly rather than deriving from
/// <c>BasePermissionRequirement</c>, whose final arm authorizes any provider managing the organization. Providers
/// manage an organization's billing and configuration, but access rules gate who can lease credentials out of it,
/// which is not theirs to change. A non-member has no organization claims and so is never authorized.
/// </remarks>
public class ManageAccessRulesRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    {
        var authorized = organizationClaims is
        { Type: OrganizationUserType.Owner }
            or { Type: OrganizationUserType.Admin }
            or { Type: OrganizationUserType.Custom, Permissions.ManageAccessRules: true };

        return Task.FromResult(authorized);
    }
}
