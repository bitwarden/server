using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;

/// <summary>
/// Handles authorization logic for OrganizationUser objects.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class OrganizationUserAuthorizationHandler : AuthorizationHandler<OrganizationUserOperationRequirement>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    private bool FlexibleCollectionsIsEnabled => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public OrganizationUserAuthorizationHandler(
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserOperationRequirement requirement)
    {
        if (!FlexibleCollectionsIsEnabled)
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var targetOrganizationId = requirement.OrganizationId;
        if (targetOrganizationId == default)
        {
            context.Fail();
            return;
        }

        var org = _currentContext.GetOrganization(targetOrganizationId);

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, OrganizationUserOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (org != null)
        {
            // Acting user is a member of the target organization, check permissions
            if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
                org.Permissions.ManageGroups ||
                org.Permissions.ManageUsers ||
                org.Permissions.EditAnyCollection ||
                org.Permissions.DeleteAnyCollection)
            {
                context.Succeed(requirement);
                return;
            }
        }
        else
        {
            // Check if acting user is a provider user for the target organization
            if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Acting user is neither a member of the target organization or a provider user, fail
        context.Fail();
    }
}
