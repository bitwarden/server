using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class GetCipherPermissionsForUserQuery : IGetCipherPermissionsForUserQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly ICipherRepository _cipherRepository;
    private readonly IApplicationCacheService _applicationCacheService;

    public GetCipherPermissionsForUserQuery(ICurrentContext currentContext, ICipherRepository cipherRepository, IApplicationCacheService applicationCacheService)
    {
        _currentContext = currentContext;
        _cipherRepository = cipherRepository;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<IDictionary<Guid, OrganizationCipherPermission>> GetByOrganization(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);
        var userId = _currentContext.UserId;

        if (org == null || !userId.HasValue)
        {
            throw new NotFoundException();
        }

        var cipherPermissions = (await _cipherRepository.GetCipherPermissionsForOrganizationAsync(organizationId, userId.Value)).ToList();

        if (await CanEditAllCiphersAsync(org))
        {
            foreach (var cipher in cipherPermissions)
            {
                cipher.Read = true;
                cipher.Edit = true;
                cipher.Manage = true;
                cipher.ViewPassword = true;
            }
        }
        else if (await CanAccessUnassignedCiphersAsync(org))
        {
            foreach (var unassignedCipher in cipherPermissions.Where(c => c.Unassigned))
            {
                unassignedCipher.Read = true;
                unassignedCipher.Edit = true;
                unassignedCipher.Manage = true;
                unassignedCipher.ViewPassword = true;
            }
        }

        return cipherPermissions.ToDictionary(c => c.Id);
    }

    private async Task<bool> CanEditAllCiphersAsync(CurrentContextOrganization org)
    {
        // Custom users with EditAnyCollection permissions can always edit all ciphers
        if (org is { Type: OrganizationUserType.Custom, Permissions.EditAnyCollection: true })
        {
            return true;
        }

        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(org.Id);

        // Owners/Admins can only edit all ciphers if the organization has the setting enabled
        if (orgAbility is { AllowAdminAccessToAllCollectionItems: true } && org is
            { Type: OrganizationUserType.Admin or OrganizationUserType.Owner })
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CanAccessUnassignedCiphersAsync(CurrentContextOrganization org)
    {
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        return false;
    }
}
