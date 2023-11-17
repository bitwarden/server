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

    private bool UseFlexibleCollections => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

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
        if (!UseFlexibleCollections)
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        if (requirement.OrganizationId == default)
        {
            return;
        }

        var org = _currentContext.GetOrganization(requirement.OrganizationId);

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
                org.Permissions.DeleteAnyCollection ||
                org.Permissions.CreateNewCollections ||
                !org.LimitCollectionCreationDeletion)
            {
                context.Succeed(requirement);
            }
        }
        else
        {
            // Check if acting user is a provider user for the target organization
            if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
            {
                context.Succeed(requirement);
            }
        }
    }
}
