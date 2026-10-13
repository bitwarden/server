using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Authorization.Ciphers;

/// <summary>
/// Authorizes organization-wide cipher operations that do not depend on the user's collection assignments.
/// </summary>
public class CipherOrganizationAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<CipherOrganizationOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CipherOrganizationOperationRequirement requirement,
        OrganizationScope resource)
    {
        if (!currentContext.UserId.HasValue)
        {
            return;
        }

        var authorized = requirement switch
        {
            not null when requirement == CipherOrganizationOperations.ReadAnyAsAdmin => await CanReadAnyAsAdminAsync(
                resource),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null)
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAnyAsAdminAsync(Guid organizationId)
    {
        var org = currentContext.GetOrganization(organizationId);
        if (org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
            { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        return await currentContext.ProviderUserForOrgAsync(organizationId);
    }
}
