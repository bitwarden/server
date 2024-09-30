#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserDetailsAuthorizationHandler
    : AuthorizationHandler<OrganizationUserUserDetailsOperationRequirement, OrganizationScope>
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public OrganizationUserUserDetailsAuthorizationHandler(ICurrentContext currentContext, IFeatureService featureService)
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserUserDetailsOperationRequirement requirement, OrganizationScope organizationScope)
    {
        var authorized = false;

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserUserDetailsOperations.ReadAll):
                authorized = await CanReadAllAsync(organizationScope);
                break;
        }

        if (authorized)
        {
            context.Succeed(requirement!);
        }
    }

    private async Task<bool> CanReadAllAsync(Guid organizationId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.Pm3478RefactorOrganizationUserApi))
        {
            return await CanReadAllAsync_vNext(organizationId);
        }

        return await CanReadAllAsync_vCurrent(organizationId);
    }

    private async Task<bool> CanReadAllAsync_vCurrent(Guid organizationId)
    {
        // All users of an organization can read all other users of that organization for collection access management
        var org = _currentContext.GetOrganization(organizationId);
        if (org is not null)
        {
            return true;
        }

        // Allow provider users to read all organization users if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(organizationId);
    }

    private async Task<bool> CanReadAllAsync_vNext(Guid organizationId)
    {
        // Admins can access this for general user management
        var organization = _currentContext.GetOrganization(organizationId);
        if (organization is
        { Type: OrganizationUserType.Owner } or
        { Type: OrganizationUserType.Admin } or
        { Permissions.ManageUsers: true })
        {
            return true;
        }

        // Allow provider users to read all organization users if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(organizationId);
    }
}
