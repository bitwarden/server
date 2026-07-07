using System.Security.Claims;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points (pre-check, state,
/// submit). The deprecated full-cipher read-back (<c>GET …/cipher</c>) is hosted by a small MVC controller
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
