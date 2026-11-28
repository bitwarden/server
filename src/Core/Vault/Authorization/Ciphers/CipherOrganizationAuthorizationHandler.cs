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
            not null when requirement == CipherOrganizationOperations.ReadTypeCountsAsAdmin =>
                await CanReadAnyAsAdminAsync(resource) || HasTypeCountsPermission(resource),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null)
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Custom permissions that grant the type counts in addition to <see cref="CanReadAnyAsAdminAsync"/>. Includes
    /// <c>AccessImportExport</c> and <c>AccessReports</c> to match <c>CiphersController.CanAccessAllCiphersAsync</c>,
    /// which gates the sibling <c>organization-details</c> routes, and <c>ManagePolicies</c> for the item type
    /// restriction policy.
    /// </summary>
    private bool HasTypeCountsPermission(Guid organizationId)
    {
        var org = currentContext.GetOrganization(organizationId);
        return org is { Permissions.ManagePolicies: true }
            or { Permissions.AccessImportExport: true }
            or { Permissions.AccessReports: true };
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
