using Bit.Api.Vault.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Pam.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

/// <summary>
/// Hosts the single deprecated full-cipher read-back endpoint. The rest of the <c>leases/ciphers/{id}</c> resource
/// (pre-check, state, submit) is served as Minimal API endpoints from the Commercial.Pam library. This action stays
/// in the Api project because it depends on the Vault response models (<see cref="CipherDetailsResponseModel"/>),
/// which live here; it is scheduled for removal, after which the Api project carries no PAM code.
/// </summary>
[Route("leases/ciphers/{id:guid}")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class CipherLeaseController(
    IUserService userService,
    IGetLeasedCipherQuery getLeasedCipherQuery,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICollectionCipherRepository collectionCipherRepository,
    ICipherLeaseGate cipherLeaseGate,
    GlobalSettings globalSettings)
    : Controller
{
    /// <summary>
    /// Returns the cipher with its complete data, but only if the caller currently holds an active lease for it.
    /// This is the read-back counterpart to the partial data sync returns for leasing-gated ciphers. The data is
    /// still client-encrypted; the lease only gates whether the server hands it over.
    /// </summary>
    // DEPRECATED: scheduled for removal; the full leased cipher will be served through the standard cipher read path
    // rather than this dedicated endpoint. Kept fully functional during the PAM pre-release. Removal is a later task.
    [Obsolete("Deprecated and scheduled for removal; the full leased cipher will be served through the standard " +
              "cipher read path instead of this dedicated endpoint. Kept functional for the PAM pre-release.")]
    [HttpGet("cipher")]
    public async Task<CipherDetailsResponseModel> GetCipher(Guid id)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new NotFoundException();
        }

        var cipher = await getLeasedCipherQuery.GetLeasedCipherAsync(user.Id, id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var organizationAbility = cipher.OrganizationId.HasValue
            ? await organizationAbilityCacheService.GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            : null;
        var collectionCiphers = await collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id);

        // The query above already confirmed an active lease, so the gate authorizes full data here.
        var access = await cipherLeaseGate.AuthorizeReadAsync(user.Id, cipher);
        return access is null
            ? new CipherDetailsResponseModel(cipher, user, organizationAbility, globalSettings, collectionCiphers)
            : new FullCipherDetailsResponseModel(access, cipher, user, organizationAbility, globalSettings, collectionCiphers);
    }
}
