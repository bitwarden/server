using Bit.Api.Models.Response;
using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

/// <summary>
/// The <c>access-requests</c> resource: lease requests through their lifecycle, before any lease is minted. Covers
/// both the requester's own queue (their requests across every organization, plus activation and withdrawal) and the
/// approver's queue (requests on collections the caller can Manage, plus the decision). Activating an approved request
/// mints a lease, which from then on lives under the <c>leases</c> resource.
/// </summary>
[ApiController]
[Route("access-requests")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class AccessRequestsController(
    IUserService userService,
    IListInboxRequestsQuery listInboxRequestsQuery,
    IListInboxHistoryQuery listInboxHistoryQuery,
    IDecideAccessRequestCommand decideAccessRequestCommand,
    IListMyAccessRequestsQuery listMyAccessRequestsQuery,
    IActivateAccessRequestCommand activateAccessRequestCommand,
    ICancelAccessRequestCommand cancelAccessRequestCommand)
    : ControllerBase
{
    /// <summary>
    /// Returns the caller's pending approver queue: requests on collections the caller can Manage that are still
    /// awaiting a decision.
    /// </summary>
    [HttpGet("inbox")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetInbox()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await listInboxRequestsQuery.GetPendingAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Returns the caller's resolved approver queue (decision history and lease outcomes) within the retention window.
    /// </summary>
    [HttpGet("history")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetHistory()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var history = await listInboxHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            history.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Returns the caller's own access requests across all their organizations, regardless of status. The client
    /// re-sorts and splits into pending/recent.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetMine()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await listMyAccessRequestsQuery.GetMineAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Approves or denies a pending lease request. The caller must be able to Manage the request's collection and may
    /// not decide their own request.
    /// </summary>
    [HttpPost("{id:guid}/decision")]
    public async Task<AccessRequestDetailsResponseModel> Decide(Guid id, AccessDecisionRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await decideAccessRequestCommand.DecideAsync(userId, id, model.ToSubmission());
        return new AccessRequestDetailsResponseModel(result);
    }

    /// <summary>
    /// Activates the caller's approved access request: mints the lease that authorizes access, spanning the
    /// request's approved window. Only the requester may activate, and only while the window is open. Repeat calls
    /// while the produced lease is live return that lease.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<AccessLeaseResponseModel> Activate(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var lease = await activateAccessRequestCommand.ActivateAsync(userId, id);
        return new AccessLeaseResponseModel(lease);
    }

    /// <summary>
    /// Revokes an access request that has not produced a lease, ending it without minting access. Caller-dependent:
    /// the requester withdrawing their own request ends it as <c>cancelled</c>; a managing approver retracting a
    /// request on a collection they manage ends it as <c>denied</c>. Either way the request must still be
    /// <c>pending</c> or an unactivated <c>approved</c> request. A request that has produced a lease (revoke the lease
    /// instead) or is otherwise resolved can no longer be revoked.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await cancelAccessRequestCommand.CancelAsync(userId, id);
        return NoContent();
    }
}
