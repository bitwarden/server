using System.Security.Claims;
using Bit.Core.Services;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points (pre-check, state,
/// submit). The deprecated full-cipher read-back (<c>GET …/cipher</c>) is hosted by a small MVC controller
/// in the Api project instead, since it depends on the Api Vault response models.
/// </summary>
public class CipherLeaseEndpointsHandler(
    IUserService userService,
    TimeProvider timeProvider,
    ISubmitAccessRequestCommand submitAccessRequestCommand)
{
    public Task<AccessPreCheckResponseModel> PreCheck(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public Task<CipherAccessStateResponseModel> State(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public async Task<AccessRequestResultResponseModel> Post(ClaimsPrincipal user, Guid id, AccessRequestCreateRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await submitAccessRequestCommand.SubmitAsync(userId, id, model.ToSubmission());
        return new AccessRequestResultResponseModel(result, timeProvider.GetUtcNow().UtcDateTime);
    }
}
