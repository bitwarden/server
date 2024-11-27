using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserMiniDetailsAuthorizationHandler :
    AuthorizationHandler<OrganizationUserUserMiniDetailsOperationRequirement, OrganizationScope>
{
    private readonly ICurrentContext _currentContext;

    public OrganizationUserUserMiniDetailsAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserUserMiniDetailsOperationRequirement requirement, OrganizationScope organizationScope)
    {
        var authorized = false;

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserUserMiniDetailsOperations.ReadAll):
                authorized = await CanReadAllAsync(organizationScope);
                break;
        }

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAllAsync(Guid organizationId)
    {
        // All organization users can access this data to manage collection access
        var organization = _currentContext.GetOrganization(organizationId);
        if (organization != null)
        {
            return true;
        }

        // Providers can also access this to manage the organization generally
        return await _currentContext.ProviderUserForOrgAsync(organizationId);
    }
}
