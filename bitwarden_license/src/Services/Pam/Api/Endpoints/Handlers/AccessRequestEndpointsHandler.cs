using System.Security.Claims;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>access-requests</c> resource. The Minimal API endpoints (see <c>AccessRequestEndpoints</c>)
/// resolve this handler from DI.
/// </summary>
public class AccessRequestEndpointsHandler(
    IUserService userService,
    TimeProvider timeProvider,
    IListInboxRequestsQuery listInboxRequestsQuery,
    IListInboxHistoryQuery listInboxHistoryQuery,
    IListMyAccessRequestsQuery listMyAccessRequestsQuery,
    IDecideAccessRequestCommand decideAccessRequestCommand,
    IActivateAccessRequestCommand activateAccessRequestCommand,
    ICancelAccessRequestCommand cancelAccessRequestCommand,
    IGetAccessRequestDetailsQuery getAccessRequestDetailsQuery)
{
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetInbox(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var requests = await listInboxRequestsQuery.GetPendingAsync(userId, timeProvider.GetUtcNow().UtcDateTime);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetHistory(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var history = await listInboxHistoryQuery.GetHistoryAsync(userId, timeProvider.GetUtcNow().UtcDateTime);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            history.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetMine(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var requests = await listMyAccessRequestsQuery.GetMineAsync(userId, timeProvider.GetUtcNow().UtcDateTime);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<AccessRequestDetailsResponseModel> GetDetails(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var details = await getAccessRequestDetailsQuery.GetDetailsAsync(userId, id, timeProvider.GetUtcNow().UtcDateTime);
        return new AccessRequestDetailsResponseModel(details);
    }

    public async Task<AccessRequestDetailsResponseModel> Decide(ClaimsPrincipal user, Guid id, AccessDecisionRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await decideAccessRequestCommand.DecideAsync(userId, id, model.ToSubmission());
        return new AccessRequestDetailsResponseModel(result);
    }

    public async Task<AccessLeaseResponseModel> Activate(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        // One clock: the same instant guards and mints the lease and derives the response status, so a successful
        // activation can never serialize as already expired.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lease = await activateAccessRequestCommand.ActivateAsync(userId, id, now);
        return new AccessLeaseResponseModel(lease, now);
    }

    public async Task Revoke(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        await cancelAccessRequestCommand.CancelAsync(userId, id);
    }
}
