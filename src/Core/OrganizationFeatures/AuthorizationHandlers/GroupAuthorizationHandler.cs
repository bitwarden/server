#nullable enable

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class GroupAuthorizationHandler : AuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;

    public GroupAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group? resource)
    {
        if (resource == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement == GroupOperations.Create:
                CanCreate(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Read:
                CanRead(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Update:
                CanUpdate(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Delete:
                CanDelete(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.ReadAll:
                CanReadAll(context, requirement, resource);
                break;
        }

        await Task.CompletedTask;
    }

    private void CanCreate(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        CanManage(context, requirement, resource);
    }

    private void CanRead(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        CanManage(context, requirement, resource);
    }

    private void CanUpdate(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        CanManage(context, requirement, resource);
    }

    private void CanDelete(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        CanManage(context, requirement, resource);
    }

    private void CanReadAll(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        var org = _currentContext.GetOrganization(resource.OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        // TODO: providers need to be included in the claims
        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        org.Type == OrganizationUserType.Manager ||
                        org.Permissions.ManageGroups ||
                        org.Permissions.EditAssignedCollections ||
                        org.Permissions.DeleteAssignedCollections ||
                        org.Permissions.CreateNewCollections ||
                        org.Permissions.EditAnyCollection ||
                        org.Permissions.DeleteAnyCollection;

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }

    private void CanManage(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        // TODO: providers need to be included in the claims
        var org = _currentContext.GetOrganization(resource.OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        org.Permissions.ManageGroups;

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
