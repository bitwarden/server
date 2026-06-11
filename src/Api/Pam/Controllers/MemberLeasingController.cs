using Bit.Api.Models.Response;
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
    ICancelAccessRequestCommand cancelAccessRequestCommand)
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
    /// Withdraws the caller's own pending access request. Only the requester may cancel, and only while the request is
    /// still pending; a resolved request can no longer be withdrawn.
    /// </summary>
    [HttpDelete("requests/{id:guid}")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await cancelAccessRequestCommand.CancelAsync(userId, id);
        return NoContent();
    }
}
