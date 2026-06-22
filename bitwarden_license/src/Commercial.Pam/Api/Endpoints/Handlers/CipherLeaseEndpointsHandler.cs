using System.Security.Claims;
using Bit.Commercial.Pam.Api.Models.Request;
using Bit.Commercial.Pam.Api.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;

namespace Bit.Commercial.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>ciphers/{id}/lease</c> resource: the per-cipher leasing entry points (pre-check, state,
/// submit). The deprecated full-cipher read-back (<c>GET …/lease/cipher</c>) is hosted by a small MVC controller
/// in the Api project instead, since it depends on the Api Vault response models.
/// </summary>
public class CipherLeaseEndpointsHandler(
    IUserService userService,
    IAccessPreCheckQuery preCheckQuery,
    IGetCipherAccessStateQuery cipherAccessStateQuery,
    ISubmitAccessRequestCommand submitAccessRequestCommand)
{
    public async Task<AccessPreCheckResponseModel> PreCheck(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await preCheckQuery.PreCheckAsync(userId, id);
        return new AccessPreCheckResponseModel(id, result);
    }

    public async Task<CipherAccessStateResponseModel> State(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await cipherAccessStateQuery.GetStateAsync(userId, id);
        return new CipherAccessStateResponseModel(result);
    }

    public async Task<AccessRequestResultResponseModel> Post(ClaimsPrincipal user, Guid id, AccessRequestCreateRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await submitAccessRequestCommand.SubmitAsync(userId, id, model.ToSubmission());
        return new AccessRequestResultResponseModel(result);
    }
}
