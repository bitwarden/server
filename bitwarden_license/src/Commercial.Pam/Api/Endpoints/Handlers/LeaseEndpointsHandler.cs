using System.Security.Claims;
using Bit.Commercial.Pam.Api.Models.Request;
using Bit.Commercial.Pam.Api.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases</c> resource. Holds the logic the <c>LeasesController</c> previously hosted; the
/// Minimal API endpoints (see <c>LeaseEndpoints</c>) are thin lambdas that resolve this handler from DI.
/// </summary>
public class LeaseEndpointsHandler(
    IUserService userService,
    IListActiveLeasesQuery listActiveLeasesQuery,
    IListLeaseHistoryQuery listLeaseHistoryQuery,
    IListMyActiveAccessLeasesQuery listMyActiveAccessLeasesQuery,
    IRevokeAccessLeaseCommand revokeAccessLeaseCommand,
    IRequestLeaseExtensionCommand requestLeaseExtensionCommand)
{
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetActive(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var leases = await listActiveLeasesQuery.GetActiveAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var leases = await listLeaseHistoryQuery.GetHistoryAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetMine(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var leases = await listMyActiveAccessLeasesQuery.GetMineActiveAsync(userId);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    public async Task Revoke(ClaimsPrincipal user, Guid id, AccessLeaseRevokeRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        await revokeAccessLeaseCommand.RevokeAsync(userId, id, model.Reason);
    }

    public async Task<AccessRequestDetailsResponseModel> Extend(ClaimsPrincipal user, Guid id, AccessLeaseExtensionRequestModel model)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var details = await requestLeaseExtensionCommand.ExtendAsync(userId, model.ToSubmission(id));
        return new AccessRequestDetailsResponseModel(details);
    }
}
