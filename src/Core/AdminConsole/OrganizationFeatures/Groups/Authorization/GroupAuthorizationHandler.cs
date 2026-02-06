#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

public class GroupAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<GroupOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement, OrganizationScope organizationScope)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name == nameof(GroupOperations.ReadAll) =>
                await CanReadAllAsync(organizationScope),
            not null when requirement.Name == nameof(GroupOperations.ReadAllDetails) =>
                await CanViewGroupDetailsAsync(organizationScope),
            _ => false
        };

        if (requirement is not null && authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAllAsync(OrganizationScope organizationScope) =>
        currentContext.GetOrganization(organizationScope) is not null
        || await currentContext.ProviderUserForOrgAsync(organizationScope);

    private async Task<bool> CanViewGroupDetailsAsync(OrganizationScope organizationScope) =>
        currentContext.GetOrganization(organizationScope) is
    { Type: OrganizationUserType.Owner } or
    { Type: OrganizationUserType.Admin } or
    {
        Permissions: { ManageGroups: true } or
        { ManageUsers: true }
    } ||
        await currentContext.ProviderUserForOrgAsync(organizationScope);
}
