#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class ManageUsersRequirement : IOrganizationRequirement;

public class ManageUserRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : OrganizationRequirementHandler<ManageUsersRequirement>(currentContext, httpContextAccessor)
{
    private readonly ICurrentContext _currentContext = currentContext;

    protected override async Task<bool> Authorize(Guid organizationId, CurrentContextOrganization? organizationClaims)
        => organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.ManageUsers: true }
            || await _currentContext.ProviderUserForOrgAsync(organizationId);
}
