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
        if (!_featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext))
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

        // Acting user is not a member of the target organization, fail
        var org = _currentContext.GetOrganization(targetOrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserOperations.Read):
                await CanReadAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAsync(AuthorizationHandlerContext context, OrganizationUserOperationRequirement requirement,
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
