using Bit.Api.Models.Response;
using Bit.Api.Pam.Models.Response;
using Bit.Core;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

/// <summary>
/// Governance surface over leases: read models of all active and recently-ended leases the caller can Manage,
/// powering the governance dashboard. Unlike the member surface on <see cref="MemberLeasingController"/> (the caller's
/// own leases), these span every member. Scope is the caller's manageable collections — the same resolution as the
/// approver inbox — so an org admin or collection manager sees all access in their scope.
/// </summary>
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class LeaseGovernanceController(
    IUserService userService,
    IListActiveLeasesQuery listActiveLeasesQuery,
    IListLeaseHistoryQuery listLeaseHistoryQuery)
    : Controller
{
    /// <summary>
    /// Returns every currently-active lease on collections the caller can Manage.
    /// </summary>
    [HttpGet("leases/active")]
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetActive()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listActiveLeasesQuery.GetActiveAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    /// <summary>
    /// Returns the ended leases (expired or revoked) on collections the caller can Manage, within the history window.
    /// </summary>
    [HttpGet("leases/history")]
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listLeaseHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }
}
