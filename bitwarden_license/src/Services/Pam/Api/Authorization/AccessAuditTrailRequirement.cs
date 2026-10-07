using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.Api.Authorization;

/// <summary>
/// Requires access to the organization's PAM audit trail: an Owner, an Admin, or a Custom user holding
/// <see cref="Permissions.AccessEventLogs"/>. Providers are not authorized.
/// </summary>
public class AccessAuditTrailRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    {
        var authorized = organizationClaims is
        { Type: OrganizationUserType.Owner }
            or { Type: OrganizationUserType.Admin }
            or { Type: OrganizationUserType.Custom, Permissions.AccessEventLogs: true };

        return Task.FromResult(authorized);
    }
}
