using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class OrganizationAuthorizationHandler : AuthorizationHandler<OrganizationOperationRequirement, CurrentContentOrganization>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement,
        CurrentContentOrganization resource)
    {
        if (requirement == OrganizationOperations.ReadAllGroups)
        {
            await ReadAllGroupsAsync(context, requirement, resource);
            return;
        }
    }

    private async Task ReadAllGroupsAsync(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement,
        CurrentContentOrganization resource)
    {
        // TODO: providers need to be included in the claims

        var canAccess = resource.Type == OrganizationUserType.Owner ||
            resource.Type == OrganizationUserType.Admin ||
            resource.Type == OrganizationUserType.Manager ||
            (resource.Permissions?.ManageGroups ?? false) ||
            (resource.Permissions?.EditAssignedCollections ?? false) ||
            (resource.Permissions?.DeleteAssignedCollections ?? false) ||
            (resource.Permissions?.CreateNewCollections ?? false) ||
            (resource.Permissions?.EditAnyCollection ?? false) ||
            (resource.Permissions?.DeleteAnyCollection ?? false);

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
