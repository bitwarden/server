using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Authorization.SecurityTasks;

public class SecurityTaskAuthorizationHandler : AuthorizationHandler<SecurityTaskOperationRequirement, SecurityTask>
{
    private readonly ICurrentContext _currentContext;
    private readonly IGetCipherPermissionsForUserQuery _getCipherPermissionsForUserQuery;

    private readonly Dictionary<Guid, IDictionary<Guid, OrganizationCipherPermission>> _cipherPermissionCache = new();

    public SecurityTaskAuthorizationHandler(ICurrentContext currentContext, IGetCipherPermissionsForUserQuery getCipherPermissionsForUserQuery)
    {
        _currentContext = currentContext;
        _getCipherPermissionsForUserQuery = getCipherPermissionsForUserQuery;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        SecurityTaskOperationRequirement requirement,
        SecurityTask task)
    {
        if (!_currentContext.UserId.HasValue)
        {
            return;
        }

        var org = _currentContext.GetOrganization(task.OrganizationId);

        if (org == null)
        {
            // User must be a member of the organization
            return;
        }

        var authorized = requirement switch
        {
            not null when requirement == SecurityTaskOperations.Read => await CanReadAsync(task, org),
            not null when requirement == SecurityTaskOperations.Create => await CanCreateAsync(task, org),
            not null when requirement == SecurityTaskOperations.Update => await CanUpdateAsync(task, org),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null)
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAsync(SecurityTask task, CurrentContextOrganization org)
    {
        if (!task.CipherId.HasValue)
        {
            // Tasks without cipher IDs are not possible currently
            return false;
        }

        if (HasAdminAccessToSecurityTasks(org))
        {
            // Admins can read any task for ciphers in the organization
            return await CipherBelongsToOrgAsync(org, task.CipherId.Value);
        }

        return await CanReadCipherForOrgAsync(org, task.CipherId.Value);
    }

    private async Task<bool> CanCreateAsync(SecurityTask task, CurrentContextOrganization org)
    {
        if (!task.CipherId.HasValue)
        {
            // Tasks without cipher IDs are not possible currently
            return false;
        }

        if (!HasAdminAccessToSecurityTasks(org))
        {
            // User must be an Admin/Owner or have custom permissions for reporting
            return false;
        }

        return await CipherBelongsToOrgAsync(org, task.CipherId.Value);
    }

    private async Task<bool> CanUpdateAsync(SecurityTask task, CurrentContextOrganization org)
    {
        if (!task.CipherId.HasValue)
        {
            // Tasks without cipher IDs are not possible currently
            return false;
        }

        // Only users that can edit the cipher can update the task
        return await CanEditCipherForOrgAsync(org, task.CipherId.Value);
    }

    private async Task<bool> CanEditCipherForOrgAsync(CurrentContextOrganization org, Guid cipherId)
    {
        var ciphers = await GetCipherPermissionsForOrgAsync(org);

        return ciphers.TryGetValue(cipherId, out var cipher) && cipher.Edit;
    }

    private async Task<bool> CanReadCipherForOrgAsync(CurrentContextOrganization org, Guid cipherId)
    {
        var ciphers = await GetCipherPermissionsForOrgAsync(org);

        return ciphers.TryGetValue(cipherId, out var cipher) && cipher.Read;
    }

    private async Task<bool> CipherBelongsToOrgAsync(CurrentContextOrganization org, Guid cipherId)
    {
        var ciphers = await GetCipherPermissionsForOrgAsync(org);

        return ciphers.ContainsKey(cipherId);
    }

    private bool HasAdminAccessToSecurityTasks(CurrentContextOrganization org)
    {
        return org is
        { Type: OrganizationUserType.Admin or OrganizationUserType.Owner } or
        { Type: OrganizationUserType.Custom, Permissions.AccessReports: true };
    }

    private async Task<IDictionary<Guid, OrganizationCipherPermission>> GetCipherPermissionsForOrgAsync(CurrentContextOrganization organization)
    {
        // Re-use permissions we've already fetched for the organization
        if (_cipherPermissionCache.TryGetValue(organization.Id, out var cachedCiphers))
        {
            return cachedCiphers;
        }

        var cipherPermissions = await _getCipherPermissionsForUserQuery.GetByOrganization(organization.Id);

        _cipherPermissionCache.Add(organization.Id, cipherPermissions);

        return cipherPermissions;
    }
}
