using Bit.Api.Models.Response;
using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Core;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class ApproverInboxController(
    IUserService userService,
    IListInboxRequestsQuery listInboxRequestsQuery,
    IListInboxHistoryQuery listInboxHistoryQuery,
    IDecideAccessRequestCommand decideAccessRequestCommand,
    IRevokeAccessLeaseCommand revokeAccessLeaseCommand)
    : Controller
{
    /// <summary>
    /// Returns the caller's pending approver queue: requests on collections the caller can Manage that are still
    /// awaiting a decision.
    /// </summary>
    [HttpGet("access-requests/inbox")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetRequests()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await listInboxRequestsQuery.GetPendingAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Returns the caller's resolved approver queue (decision history and lease outcomes) within the retention window.
    /// </summary>
    [HttpGet("access-requests/history")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetHistory()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var history = await listInboxHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            history.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Approves or denies a pending lease request. The caller must be able to Manage the request's collection and may
    /// not decide their own request.
    /// </summary>
    [HttpPost("access-requests/{id:guid}/decision")]
    public async Task<AccessRequestDetailsResponseModel> Decide(Guid id, [FromBody] AccessDecisionRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await decideAccessRequestCommand.DecideAsync(userId, id, model.ToSubmission());
        return new AccessRequestDetailsResponseModel(result);
    }

    /// <summary>
    /// Revokes an active lease early. The caller must be able to Manage the lease's collection.
    /// </summary>
    [HttpPost("leases/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] AccessLeaseRevokeRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await revokeAccessLeaseCommand.RevokeAsync(userId, id, model.Reason);
        return NoContent();
    }
}
