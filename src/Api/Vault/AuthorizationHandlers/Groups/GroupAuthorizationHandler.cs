using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Groups;

/// <summary>
/// Handles authorization logic for Group objects.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class GroupAuthorizationHandler : BulkAuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public GroupAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement, ICollection<Group> resources)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext))
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

        // Establish pattern of authorization handler null checking passed resources
        if (resources == null)
        {
            context.Fail();
            return;
        }

        if (!resources.Any())
        {
            context.Succeed(requirement);
            return;
        }

        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var targetOrganizationId = resources.First().OrganizationId;

        // Ensure all target collections belong to the same organization
        if (resources.Any(tc => tc.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested groups must belong to the same organization.");
        }

        // Acting user is not a member of the target organization, fail
        var org = _currentContext.GetOrganization(targetOrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement == GroupOperations.Read:
                await CanReadAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions.ManageGroups ||
            org.Permissions.ManageUsers ||
            org.Permissions.EditAnyCollection ||
            org.Permissions.DeleteAnyCollection ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }
}
