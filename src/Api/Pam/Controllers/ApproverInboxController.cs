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

[Route("leasing")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class ApproverInboxController(
    IUserService userService,
    IGetInboxRequestsQuery getInboxRequestsQuery,
    IGetInboxHistoryQuery getInboxHistoryQuery,
    IDecideLeaseRequestCommand decideLeaseRequestCommand,
    IRevokeLeaseCommand revokeLeaseCommand)
    : Controller
{
    /// <summary>
    /// Returns the caller's pending approver queue: requests on collections the caller can Manage that are still
    /// awaiting a decision.
    /// </summary>
    [HttpGet("inbox/requests")]
    public async Task<ListResponseModel<InboxAccessRequestResponseModel>> GetRequests()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await getInboxRequestsQuery.GetPendingAsync(userId);
        return new ListResponseModel<InboxAccessRequestResponseModel>(
            requests.Select(r => new InboxAccessRequestResponseModel(r)));
    }

    /// <summary>
    /// Returns the caller's resolved approver queue (decision history and lease outcomes) within the retention window.
    /// </summary>
    [HttpGet("inbox/history")]
    public async Task<ListResponseModel<InboxAccessRequestResponseModel>> GetHistory()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var history = await getInboxHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<InboxAccessRequestResponseModel>(
            history.Select(r => new InboxAccessRequestResponseModel(r)));
    }

    /// <summary>
    /// Approves or denies a pending lease request. The caller must be able to Manage the request's collection and may
    /// not decide their own request.
    /// </summary>
    [HttpPost("requests/{id:guid}/decision")]
    public async Task<InboxAccessRequestResponseModel> Decide(Guid id, [FromBody] LeaseDecisionRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await decideLeaseRequestCommand.DecideAsync(userId, id, model.ToSubmission());
        return new InboxAccessRequestResponseModel(result);
    }

    /// <summary>
    /// Revokes an active lease early. The caller must be able to Manage the lease's collection.
    /// </summary>
    [HttpPost("leases/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] LeaseRevokeRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await revokeLeaseCommand.RevokeAsync(userId, id, model.Reason);
        return NoContent();
    }
}
