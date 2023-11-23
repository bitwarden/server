#nullable enable
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

/// <summary>
/// Handles authorization logic for Collection operations.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class CollectionAuthorizationHandler : AuthorizationHandler<CollectionOperationRequirement>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private Guid _targetOrganizationId;

    private bool UseFlexibleCollections => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public CollectionAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement)
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
            case not null when requirement.Name == nameof(CollectionOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;

            case not null when requirement.Name == nameof(CollectionOperations.ReadAllWithAccess):
                await CanReadAllWithAccessAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (org != null)
        {
            // Acting user is a member of the target organization, check permissions
            if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
                  org.Permissions.EditAnyCollection ||
                  org.Permissions.DeleteAnyCollection ||
                  org.Permissions.AccessImportExport)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Check if acting user is a provider user for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadAllWithAccessAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (org != null)
        {
            // Acting user is a member of the target organization, check permissions
            if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
                org.Permissions.EditAnyCollection ||
                org.Permissions.DeleteAnyCollection)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Check if acting user is a provider user for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }
}
