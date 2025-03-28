#nullable enable

using Bit.Api.AdminConsole.Context;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ManageUsersRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        Guid organizationId,
        CurrentContextOrganization? organizationClaims,
        IProviderOrganizationContext providerOrganizationContext)
        => organizationClaims is
    { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
    { Permissions.ManageUsers: true }
            || await providerOrganizationContext.ProviderUserForOrgAsync(organizationId);
}
