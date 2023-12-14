#nullable enable
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
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
    private readonly IApplicationCacheService _applicationCacheService;

    private bool FlexibleCollectionsIsEnabled => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public GroupAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService,
        IApplicationCacheService applicationCacheService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
        _applicationCacheService = applicationCacheService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement)
    {
        if (!FlexibleCollectionsIsEnabled)
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
        // Owners, Admins, and users with any of ManageGroups, ManageUsers, EditAnyCollection, DeleteAnyCollection, CreateNewCollections permissions can always read all groups
        if (org is
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

        // If the limit collection management setting is disabled, allow any user to read all groups
        if (await GetOrganizationAbilityAsync(requirement.OrganizationId) is { LimitCollectionCreationDeletion: false })
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

    private async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId)
    {
        (await _applicationCacheService.GetOrganizationAbilitiesAsync())
            .TryGetValue(orgId, out var organizationAbility);

        return organizationAbility;
    }
}
