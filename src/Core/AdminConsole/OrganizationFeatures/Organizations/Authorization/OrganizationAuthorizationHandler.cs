#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Authorization;

public class OrganizationAuthorizationHandler
    : AuthorizationHandler<OrganizationOperationRequirement, OrganizationScope>
{
    private readonly ICurrentContext _currentContext;

    public OrganizationAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationOperationRequirement requirement, OrganizationScope organizationScope)
    {
        var authorized = false;

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationOperations.Update):
                authorized = await CanUpdateAsync(organizationScope);
                break;
        }

        if (authorized)
        {
            context.Succeed(requirement!);
        }
    }

    private async Task<bool> CanUpdateAsync(Guid organizationId)
    {
        var organization = _currentContext.GetOrganization(organizationId);
        if (organization != null)
        {
            return true;
        }

        // Allow provider users to update organization data if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(organizationId);
    }
}
