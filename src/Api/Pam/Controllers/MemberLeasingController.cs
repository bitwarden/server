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

/// <summary>
/// Caller-scoped leasing surface: a user's own access requests and active leases, spanning every organization they
/// belong to, plus activation of their approved requests. Distinct from the approver-facing surface on
/// <see cref="ApproverInboxController"/>. Both share the <c>leasing</c> route prefix; the templates don't overlap.
/// </summary>
[Route("leasing")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class MemberLeasingController(
    IUserService userService,
    IListMyAccessRequestsQuery listMyAccessRequestsQuery,
    IListMyActiveAccessLeasesQuery listMyActiveAccessLeasesQuery,
    IActivateAccessRequestCommand activateAccessRequestCommand,
    ICancelAccessRequestCommand cancelAccessRequestCommand,
    IRequestLeaseExtensionCommand requestLeaseExtensionCommand)
    : Controller
{
    /// <summary>
    /// Returns the caller's own access requests across all their organizations, regardless of status. The client
    /// re-sorts and splits into pending/recent.
    /// </summary>
    [HttpGet("requests/mine")]
    public async Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetMyRequests()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await listMyAccessRequestsQuery.GetMineAsync(userId);
        return new ListResponseModel<AccessRequestDetailsResponseModel>(
            requests.Select(r => new AccessRequestDetailsResponseModel(r)));
    }

    /// <summary>
    /// Returns the caller's currently-active leases across all their organizations.
    /// </summary>
    [HttpGet("leases/mine/active")]
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetMyActiveLeases()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listMyActiveAccessLeasesQuery.GetMineActiveAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    /// <summary>
    /// Activates the caller's approved access request: mints the lease that authorizes access, spanning the
    /// request's approved window. Only the requester may activate, and only while the window is open. Repeat calls
    /// while the produced lease is live return that lease.
    /// </summary>
    [HttpPost("requests/{id:guid}/activate")]
    public async Task<AccessLeaseResponseModel> Activate(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var lease = await activateAccessRequestCommand.ActivateAsync(userId, id);
        return new AccessLeaseResponseModel(lease);
    }

    /// <summary>
    /// Cancels an access request that has not produced a lease. The requester may cancel their own request, and a
    /// managing approver may cancel any request on a collection they manage; either way the request must still be
    /// <c>pending</c> or an unactivated <c>approved</c> request. A request that has produced a lease (revoke the lease
    /// instead) or is otherwise resolved can no longer be cancelled.
    /// </summary>
    [HttpDelete("requests/{id:guid}")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await cancelAccessRequestCommand.CancelAsync(userId, id);
        return NoContent();
    }

    /// <summary>
    /// Extends one of the caller's active leases by the requested duration. Extensions are always auto-approved,
    /// subject to the governing rule allowing them and the per-lease maximum not being reached; the lease's end is
    /// pushed out in place rather than minting a new lease. Only the lease's requester may extend it.
    /// </summary>
    [HttpPost("requests/extension")]
    public async Task<AccessRequestDetailsResponseModel> RequestExtension([FromBody] AccessLeaseExtensionRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var details = await requestLeaseExtensionCommand.ExtendAsync(userId, model.ToSubmission());
        return new AccessRequestDetailsResponseModel(details);
    }
}
