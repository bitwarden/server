using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Services;

public class CipherPermissionsService : ICipherPermissionsService
{
    private readonly IApplicationCacheService _applicationCacheService;

    public CipherPermissionsService(IApplicationCacheService applicationCacheService)
    {
        _applicationCacheService = applicationCacheService;
    }

    public async Task<CipherPermissionsResponseData> GetCipherPermissionsAsync(Cipher cipher, User user)
    {
        var canDeleteOrRestore = await CanDeleteOrRestoreAsync(cipher, user);

        return new CipherPermissionsResponseData(
            Delete: canDeleteOrRestore,
            Restore: canDeleteOrRestore);
    }

    public async Task<IDictionary<Guid, CipherPermissionsResponseData>> GetManyCipherPermissionsAsync(IEnumerable<Cipher> ciphers, User user)
    {
        var permissionTasks = ciphers.Select(async cipher => new
        {
            cipher.Id,
            Permissions = await GetCipherPermissionsAsync(cipher, user)
        });

        var results = await Task.WhenAll(permissionTasks);
        return results.ToDictionary(x => x.Id, x => x.Permissions);
    }

    private async Task<bool> CanDeleteOrRestoreAsync(Cipher cipher, User user)
    {
        if (user.Id == cipher.UserId)
        {
            return true;
        }

        if (!cipher.OrganizationId.HasValue)
        {
            throw new Exception("Cipher needs to belong to the user or an organization.");
        }

        if (cipher is not CipherDetails cipherDetails)
        {
            throw new ArgumentException("Cipher must be a CipherDetails instance for organization permission checks.", nameof(cipher));
        }

        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value);

        if (organizationAbilities == null)
        {
            throw new Exception("Organization ability not found.");
        }

        if (organizationAbilities.LimitItemDeletion)
        {
            return cipherDetails.Manage;
        }

        return cipherDetails.Manage || cipherDetails.Edit;
    }
}
