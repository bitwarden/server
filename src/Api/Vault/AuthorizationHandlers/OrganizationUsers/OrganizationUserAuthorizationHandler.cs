#nullable enable
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;

/// <summary>
/// Handles authorization logic for OrganizationUser objects.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class OrganizationUserAuthorizationHandler : AuthorizationHandler<OrganizationUserOperationRequirement>
{
    private readonly ICurrentContext _currentContext;

    public OrganizationUserAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserOperationRequirement requirement)
    {
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
            case not null when requirement.Name == nameof(OrganizationUserOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;
        }
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, OrganizationUserOperationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // All users of an organization can read all other users of that organization for collection access management
        if (org is not null)
        {
            context.Succeed(requirement);
        }

        // Allow provider users to read all organization users if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(requirement.OrganizationId))
        {
            context.Succeed(requirement);
        }
    }
}
