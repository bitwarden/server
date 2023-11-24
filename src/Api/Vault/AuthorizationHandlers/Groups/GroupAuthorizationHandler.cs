#nullable enable
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Groups;

/// <summary>
/// Handles authorization logic for Group operations.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class GroupAuthorizationHandler : AuthorizationHandler<GroupOperationRequirement>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    private bool UseFlexibleCollections => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public GroupAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement)
    {
        if (!UseFlexibleCollections)
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

        // Acting user is not authenticated, fail
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        if (requirement.OrganizationId == default)
        {
            context.Fail();
            return;
        }

        var org = _currentContext.GetOrganization(requirement.OrganizationId);

        switch (requirement)
        {
            case not null when requirement.Name == nameof(GroupOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // If the limit collection management setting is disabled, allow any user to read all groups
        // Otherwise, Owners, Admins, and users with any of ManageGroups, ManageUsers, EditAnyCollection, DeleteAnyCollection, CreateNewCollections permissions can always read all groups
        if (org is
        { LimitCollectionCreationDeletion: false } or
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.ManageGroups: true } or
        { Permissions.ManageUsers: true } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.CreateNewCollections: true })
        {
            context.Succeed(requirement);
            return;
        }

        // Allow provider users to read all groups if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }
}
