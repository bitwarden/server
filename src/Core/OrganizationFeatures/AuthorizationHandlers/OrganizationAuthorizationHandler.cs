using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class OrganizationAuthorizationHandler : AuthorizationHandler<OrganizationOperationRequirement, CurrentContentOrganization>
{
    private readonly ICurrentContext _currentContext;

    public OrganizationAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement,
        CurrentContentOrganization? resource)
    {
        if (resource == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement == OrganizationOperations.ReadAllGroups:
                await ReadAllGroupsAsync(context, requirement, resource);
                break;
        }

        await Task.CompletedTask;
    }

    private async Task ReadAllGroupsAsync(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement,
        CurrentContentOrganization resource)
    {
        var canAccess = resource.Type == OrganizationUserType.Owner ||
                        resource.Type == OrganizationUserType.Admin ||
                        resource.Type == OrganizationUserType.Manager ||
                        resource.Permissions.ManageGroups ||
                        resource.Permissions.EditAssignedCollections ||
                        resource.Permissions.DeleteAssignedCollections ||
                        resource.Permissions.CreateNewCollections ||
                        resource.Permissions.EditAnyCollection ||
                        resource.Permissions.DeleteAnyCollection ||
                        await _currentContext.ProviderUserForOrgAsync(resource.Id);

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
