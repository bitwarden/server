using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserMiniDetailsAuthorizationHandler :
    AuthorizationHandler<OrganizationUserUserMiniDetailsOperationRequirement, OrganizationIdResource>
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;

    public OrganizationUserUserMiniDetailsAuthorizationHandler(
        IApplicationCacheService applicationCacheService,
        ICurrentContext currentContext)
    {
        _applicationCacheService = applicationCacheService;
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserUserMiniDetailsOperationRequirement requirement, OrganizationIdResource organizationId)
    {
        var authorized = false;

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserUserMiniDetailsOperations.ReadAll):
                authorized = await CanReadAllForOrganization(organizationId);
                break;
        }

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAllForOrganization(Guid organizationId)
    {
        var organization = _currentContext.GetOrganization(organizationId);

        // Most admin types need this for administrative functionality
        if (organization is { Type: OrganizationUserType.Owner } or
            { Type: OrganizationUserType.Admin } or
            { Permissions.AccessEventLogs: true } or
            { Permissions.ManageGroups: true } or
            { Permissions.ManageUsers: true } or
            { Permissions.CreateNewCollections: true })
        {
            return true;
        }

        // Needed for creating and managing collections - this may allow all members to access this
        if (organization != null)
        {
            var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organization.Id);
            if (orgAbility is { LimitCollectionCreationDeletion: false })
            {
                return true;
            }
        }

        // Providers for the org
        return await _currentContext.ProviderUserForOrgAsync(organizationId);
    }
}
