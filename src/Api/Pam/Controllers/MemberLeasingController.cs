using Bit.Api.Pam.Models.Response;
using Bit.Core;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

/// <summary>
/// Caller-scoped leasing reads: a user's own access requests and active leases, spanning every organization they
/// belong to. Distinct from the approver-facing surface on <see cref="ApproverInboxController"/>. Both share the
/// <c>leasing</c> route prefix; the templates don't overlap.
/// </summary>
[Route("leasing")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class MemberLeasingController(
    IUserService userService,
    IListMyAccessRequestsQuery listMyAccessRequestsQuery,
    IListMyActiveLeasesQuery listMyActiveLeasesQuery)
    : Controller
{
    /// <summary>
    /// Returns the caller's own access requests across all their organizations, regardless of status, as a plain
    /// array. The client re-sorts and splits into pending/recent.
    /// </summary>
    [HttpGet("requests/mine")]
    public async Task<IEnumerable<InboxAccessRequestResponseModel>> GetMyRequests()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var requests = await listMyAccessRequestsQuery.GetMineAsync(userId);
        return requests.Select(r => new InboxAccessRequestResponseModel(r));
    }

    /// <summary>
    /// Returns the caller's currently-active leases across all their organizations as a plain array.
    /// </summary>
    [HttpGet("leases/mine/active")]
    public async Task<IEnumerable<MemberLeaseResponseModel>> GetMyActiveLeases()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listMyActiveLeasesQuery.GetMineActiveAsync(userId);
        return leases.Select(l => new MemberLeaseResponseModel(l));
    }
}
