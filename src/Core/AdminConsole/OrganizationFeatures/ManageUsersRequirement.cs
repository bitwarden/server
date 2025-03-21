#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class ManageUsersRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(Guid organizationId, CurrentContextOrganization? organizationClaims, ICurrentContext currentContext)
        => organizationClaims is
    { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
    { Permissions.ManageUsers: true }
            || await currentContext.ProviderUserForOrgAsync(organizationId);
}
