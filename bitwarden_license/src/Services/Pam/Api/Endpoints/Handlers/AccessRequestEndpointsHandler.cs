using System.Security.Claims;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>access-requests</c> resource. The Minimal API endpoints (see <c>AccessRequestEndpoints</c>)
/// resolve this handler from DI.
/// </summary>
public class AccessRequestEndpointsHandler(
    IUserService userService,
    IListInboxRequestsQuery listInboxRequestsQuery,
    IListInboxHistoryQuery listInboxHistoryQuery,
    IDecideAccessRequestCommand decideAccessRequestCommand,
    IListMyAccessRequestsQuery listMyAccessRequestsQuery,
    IActivateAccessRequestCommand activateAccessRequestCommand,
    ICancelAccessRequestCommand cancelAccessRequestCommand,
    IGetAccessRequestDetailsQuery getAccessRequestDetailsQuery)
{
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetInbox(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var requests = await listInboxRequestsQuery.GetPendingAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetHistory(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var history = await listInboxHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            history.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetMine(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var requests = await listMyAccessRequestsQuery.GetMineAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    public async Task<AccessRequestDetailsResponseModel> GetDetails(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var details = await getAccessRequestDetailsQuery.GetDetailsAsync(userId, id);
        return new AccessRequestDetailsResponseModel(details);
    }

    public async Task<Results<Ok<AccessRequestDetailsResponseModel>, PamErrorResult>> Decide(
        ClaimsPrincipal user, Guid id, AccessDecisionRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await decideAccessRequestCommand.DecideAsync(userId, id, model.ToSubmission());
        return PamResults.Ok(result, decided => new AccessRequestDetailsResponseModel(decided));
    }

    public async Task<Results<Ok<AccessLeaseResponseModel>, PamErrorResult>> Activate(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var result = await activateAccessRequestCommand.ActivateAsync(userId, id);
        return PamResults.Ok(result, lease => new AccessLeaseResponseModel(lease));
    }

    public async Task<Results<NoContent, PamErrorResult>> Revoke(ClaimsPrincipal user, Guid id)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        return PamResults.NoContent(await cancelAccessRequestCommand.CancelAsync(userId, id));
    }
}
