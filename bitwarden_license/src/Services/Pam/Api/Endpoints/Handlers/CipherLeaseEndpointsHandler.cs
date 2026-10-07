using System.Security.Claims;
using Bit.Core.Services;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>Handler for the <c>leases/ciphers/{id}</c> resource.</summary>
public class CipherLeaseEndpointsHandler(
    IUserService userService,
    TimeProvider timeProvider,
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
        return new AccessRequestResultResponseModel(result, timeProvider.GetUtcNow().UtcDateTime);
    }
}
