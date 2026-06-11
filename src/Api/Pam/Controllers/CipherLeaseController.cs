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
    IGetCipherAccessStateQuery cipherAccessStateQuery,
    ISubmitAccessRequestCommand submitAccessRequestCommand,
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
    /// Returns a single snapshot of the caller's lease state for this cipher — their active lease, pending request,
    /// and approved-but-not-yet-activated request, if any — powering the cipher-view banner and the vault-row badge.
    /// Side-effect free.
    /// </summary>
    [HttpGet("state")]
    public async Task<CipherAccessStateResponseModel> State(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await cipherAccessStateQuery.GetStateAsync(userId, id);
        return new CipherAccessStateResponseModel(result);
    }

    /// <summary>
    /// Submits a request to lease this cipher. The automatic path creates an already-approved request the requester
    /// then activates to start the lease; the human path creates a pending request for an approver. Neither mints a
    /// lease here — the requester activates the approved request (POST <c>leasing/requests/{id}/activate</c>).
    /// </summary>
    [HttpPost("")]
    public async Task<AccessRequestResultResponseModel> Post(Guid id, [FromBody] AccessRequestCreateRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await submitAccessRequestCommand.SubmitAsync(userId, id, model.ToSubmission());
        return new AccessRequestResultResponseModel(result);
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
