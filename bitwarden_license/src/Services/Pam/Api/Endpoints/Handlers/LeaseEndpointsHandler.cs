using System.Security.Claims;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases</c> resource. Holds the logic the <c>LeasesController</c> previously hosted; the
/// Minimal API endpoints (see <c>LeaseEndpoints</c>) are thin lambdas that resolve this handler from DI.
/// </summary>
public class LeaseEndpointsHandler(
    IUserService userService,
    TimeProvider timeProvider,
    IListActiveLeasesQuery listActiveLeasesQuery,
    IListLeaseHistoryQuery listLeaseHistoryQuery,
    IAccessLeaseRepository accessLeaseRepository,
    IRevokeAccessLeaseCommand revokeAccessLeaseCommand,
    IRequestLeaseExtensionCommand requestLeaseExtensionCommand)
{
    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetActive(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leases = await listActiveLeasesQuery.GetActiveAsync(userId, now);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l, now)));
    }

    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leases = await listLeaseHistoryQuery.GetHistoryAsync(userId, now);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l, now)));
    }

    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetMine(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leases = await accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now);
        return new ListResponseModel<AccessLeaseResponseModel>(
            leases.Select(l => new AccessLeaseResponseModel(l, now)));
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
