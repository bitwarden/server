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

        var authorized = requirement switch
        {
            not null when requirement == SecurityTaskOperations.Read => await CanReadAsync(task),
            not null when requirement == SecurityTaskOperations.Create => await CanCreateAsync(task),
            not null when requirement == SecurityTaskOperations.Update => await CanUpdateAsync(task),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null)
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanReadAsync(SecurityTask task)
    {
        var org = _currentContext.GetOrganization(task.OrganizationId);

        if (org == null)
        {
            // The user does not belong to the organization
            return false;
        }

        if (task.CipherId.HasValue)
        {
            return await CanReadCipherForOrgAsync(org, task.CipherId.Value);
        }

        return true;
    }

    private async Task<bool> CanCreateAsync(SecurityTask task)
    {
        var org = _currentContext.GetOrganization(task.OrganizationId);

        // User must be an Admin/Owner or have custom permissions for reporting
        if (org is
            not ({ Type: OrganizationUserType.Admin or OrganizationUserType.Owner } or
            { Permissions.EditAnyCollection: true } or
            { Permissions.AccessReports: true }))
        {
            return false;
        }

        if (task.CipherId.HasValue)
        {
            return await CipherBelongsToOrgAsync(org, task.CipherId.Value);
        }

        return true;
    }

    private async Task<bool> CanUpdateAsync(SecurityTask task)
    {
        var org = _currentContext.GetOrganization(task.OrganizationId);

        if (org == null)
        {
            // The user does not belong to the organization
            return false;
        }

        if (task.CipherId.HasValue)
        {
            // Updating a cipher task requires edit access to the cipher
            return await CanEditCipherForOrgAsync(org, task.CipherId.Value);
        }

        return true;
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
