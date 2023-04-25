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
        switch (requirement)
        {
            case not null when requirement == OrganizationOperations.ReadAllGroups:
                ReadAllGroups(context, requirement, resource);
                break;
        }

        await Task.CompletedTask;
    }

    private void ReadAllGroups(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement,
        CurrentContentOrganization resource)
    {
        // TODO: providers need to be included in the claims
        var canAccess = resource.Type == OrganizationUserType.Owner ||
                        resource.Type == OrganizationUserType.Admin ||
                        resource.Type == OrganizationUserType.Manager ||
                        resource.Permissions.ManageGroups ||
                        resource.Permissions.EditAssignedCollections ||
                        resource.Permissions.DeleteAssignedCollections ||
                        resource.Permissions.CreateNewCollections ||
                        resource.Permissions.EditAnyCollection ||
                        resource.Permissions.DeleteAnyCollection;

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
