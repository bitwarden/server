#nullable enable

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

internal class GroupAuthorizationHandler : CombinedAuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;

    public GroupAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group resource)
    {
        switch (requirement)
        {
            case not null when requirement == GroupOperations.Create:
                await CanCreateAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Read:
                await CanReadAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Update:
                await CanUpdateAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Delete:
                await CanDeleteAsync(context, requirement, resource);
                break;
        }
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement)
    {
        switch (requirement)
        {
            case not null when requirement.Name == nameof(GroupOperations.ReadAllForOrganization):
                await ReadAllGroupsAsync(context, requirement);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        Group resource) => await CanManageAsync(context, requirement, resource);

    private async Task CanReadAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        Group resource) => await CanManageAsync(context, requirement, resource);

    private async Task CanUpdateAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        Group resource) => await CanManageAsync(context, requirement, resource);

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        Group resource) => await CanManageAsync(context, requirement, resource);

    private async Task CanManageAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        Group resource)
    {
        var org = _currentContext.GetOrganization(resource.OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        var success = org.Type == OrganizationUserType.Owner ||
                      org.Type == OrganizationUserType.Admin ||
                      org.Permissions.ManageGroups ||
                      await _currentContext.ProviderUserForOrgAsync(org.Id);

        if (success)
        {
            context.Succeed(requirement);
        }
    }

    private async Task ReadAllGroupsAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement)
    {
        var org = _currentContext.GetOrganization(requirement.OrganizationId.GetValueOrDefault());
        if (org == null)
        {
            context.Fail();
            return;
        }

        var success = org.Type == OrganizationUserType.Owner ||
                      org.Type == OrganizationUserType.Admin ||
                      org.Type == OrganizationUserType.Manager ||
                      org.Permissions.ManageGroups ||
                      org.Permissions.EditAssignedCollections ||
                      org.Permissions.DeleteAssignedCollections ||
                      org.Permissions.CreateNewCollections ||
                      org.Permissions.EditAnyCollection ||
                      org.Permissions.DeleteAnyCollection ||
                      await _currentContext.ProviderUserForOrgAsync(org.Id);

        if (success)
        {
            context.Succeed(requirement);
        }
    }
}
