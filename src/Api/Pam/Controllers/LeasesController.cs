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
/// The <c>leases</c> resource: active and ended leases — the access a lease authorizes once an approved request is
/// activated. Covers the caller's own leases (across every organization), the governance surface over every lease on
/// collections the caller can Manage, and the actions on a single lease: ending it early (revoke) and extending it.
/// The governance scope mirrors the approver inbox — the caller's manageable collections — so an org admin or
/// collection manager sees all access in their scope.
/// </summary>
[ApiController]
[Route("leases")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class LeasesController(
    IUserService userService,
    IListActiveLeasesQuery listActiveLeasesQuery,
    IListLeaseHistoryQuery listLeaseHistoryQuery,
    IListMyActiveAccessLeasesQuery listMyActiveAccessLeasesQuery,
    IRevokeAccessLeaseCommand revokeAccessLeaseCommand,
    IRequestLeaseExtensionCommand requestLeaseExtensionCommand)
    : ControllerBase
{
    /// <summary>
    /// Returns every currently-active lease on collections the caller can Manage.
    /// </summary>
    [HttpGet("active")]
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
    [HttpGet("history")]
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listLeaseHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    /// <summary>
    /// Returns the caller's currently-active leases across all their organizations.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetMine()
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var leases = await listMyActiveAccessLeasesQuery.GetMineActiveAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    /// <summary>
    /// Ends an active lease early. The caller must be either the lease's holder (ending their own access) or able to
    /// Manage the lease's collection.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, AccessLeaseRevokeRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        await revokeAccessLeaseCommand.RevokeAsync(userId, id, model.Reason);
        return NoContent();
    }

    /// <summary>
    /// Extends one of the caller's active leases by the requested duration. Extensions are always auto-approved,
    /// subject to the governing rule allowing them and the per-lease maximum not being reached; the lease's end is
    /// pushed out in place rather than minting a new lease. Only the lease's requester may extend it.
    /// </summary>
    [HttpPost("{id:guid}/extend")]
    public async Task<AccessRequestDetailsResponseModel> Extend(Guid id, AccessLeaseExtensionRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var details = await requestLeaseExtensionCommand.ExtendAsync(userId, model.ToSubmission(id));
        return new AccessRequestDetailsResponseModel(details);
    }
}
