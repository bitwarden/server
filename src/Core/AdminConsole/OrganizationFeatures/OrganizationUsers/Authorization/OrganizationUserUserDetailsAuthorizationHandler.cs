#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserDetailsAuthorizationHandler : AuthorizationHandler<OrganizationUserUserDetailsOperationRequirement, OrganizationIdResource>
{
    private readonly ICurrentContext _currentContext;

    public OrganizationUserUserDetailsAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserUserDetailsOperationRequirement requirement, OrganizationIdResource organizationId)
    {
        var authorized = false;

        switch (requirement)
        {
            case not null when requirement.Name == nameof(OrganizationUserUserDetailsOperations.ReadAll):
                authorized = await CanReadAllAsync(organizationId);
                break;
        }

        if (authorized)
        {
            context.Succeed(requirement!);
        }
    }

    private async Task<bool> CanReadAllAsync(OrganizationIdResource organizationId)
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
