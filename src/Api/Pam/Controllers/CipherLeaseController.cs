using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

[Route("ciphers/{id:guid}/lease")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class CipherLeaseController(
    IUserService userService,
    IAccessPreCheckQuery preCheckQuery,
    IGetCipherLeaseStateQuery cipherLeaseStateQuery,
    IRequestAccessCommand requestAccessCommand,
    IGetLeasedCipherQuery getLeasedCipherQuery,
    IApplicationCacheService applicationCacheService,
    ICollectionCipherRepository collectionCipherRepository,
    GlobalSettings globalSettings)
    : Controller
{
    /// <summary>
    /// Reports whether leasing this cipher would be approved automatically or require human approval, so the client
    /// can present the appropriate workflow. No request is created.
    /// </summary>
    [HttpGet("pre-check")]
    public async Task<AccessPreCheckResponseModel> PreCheck(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await preCheckQuery.PreCheckAsync(userId, id);
        return new AccessPreCheckResponseModel(id, result);
    }

    /// <summary>
    /// Returns a single snapshot of the caller's lease state for this cipher — their active lease and pending request,
    /// if any — powering the cipher-view banner and the vault-row badge. Side-effect free.
    /// </summary>
    [HttpGet("state")]
    public async Task<CipherLeaseStateResponseModel> State(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await cipherLeaseStateQuery.GetStateAsync(userId, id);
        return new CipherLeaseStateResponseModel(result);
    }

    /// <summary>
    /// Submits a request to lease this cipher. The automatic path issues an active lease immediately; the human path
    /// creates a pending request for an approver.
    /// </summary>
    [HttpPost("")]
    public async Task<AccessRequestResponseModel> Post(Guid id, [FromBody] AccessRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await requestAccessCommand.RequestAccessAsync(userId, id, model.ToSubmission());
        return new AccessRequestResponseModel(result);
    }

    /// <summary>
    /// Returns the cipher with its complete data, but only if the caller currently holds an active lease for it.
    /// This is the read-back counterpart to the partial data sync returns for leasing-gated ciphers. The data is
    /// still client-encrypted; the lease only gates whether the server hands it over.
    /// </summary>
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
            ? await applicationCacheService.GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            : null;
        var collectionCiphers = await collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id);

        return new CipherDetailsResponseModel(cipher, user, organizationAbility, globalSettings, collectionCiphers);
    }
}
