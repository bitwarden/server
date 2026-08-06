using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases</c> resource. The Minimal API endpoints (see <c>LeaseEndpoints</c>) resolve this
/// handler from DI.
/// </summary>
/// <remarks>
/// The approver-governance surface (active/history views over manageable collections, extension) is deferred with
/// the rest of the approver/governance slice. Until it lands, <see cref="GetActive"/> mirrors <see cref="GetMine"/>
/// — both scoped to the caller's own leases — rather than the POC's collection-governance view.
/// </remarks>
public class LeaseEndpointsHandler(
    ICurrentContext currentContext,
    IAccessLeaseRepository accessLeaseRepository,
    TimeProvider timeProvider)
{
    public Task<ListResponseModel<AccessLeaseResponseModel>> GetActive(ClaimsPrincipal user) => GetMine(user);

    public Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public async Task<ListResponseModel<AccessLeaseResponseModel>> GetMine(ClaimsPrincipal user)
    {
        var userId = currentContext.UserId!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leases = await accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now);
        return new ListResponseModel<AccessLeaseResponseModel>(leases.Select(l => new AccessLeaseResponseModel(l)));
    }

    public async Task Revoke(ClaimsPrincipal user, Guid id, AccessLeaseRevokeRequestModel model)
    {
        var userId = currentContext.UserId!.Value;

        var lease = await accessLeaseRepository.GetByIdAsync(id);

        // 404 for both missing and someone else's lease, so the caller can't probe for leases they don't hold. The
        // approver path (ending a lease via collection-Manage rights) is deferred with the rest of the governance
        // slice, so only the lease's own holder may end it in this cut.
        if (lease is null || lease.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        if (lease.Status != AccessLeaseStatus.Active)
        {
            throw new ConflictException("This lease is not active.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The reason has no dedicated column, so it is preserved as a human decision against the originating
        // request — the holder ending their own lease is recorded as their own Deny verdict.
        var auditDecision = new AccessDecision
        {
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = userId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason,
            CreationDate = now,
        };
        auditDecision.SetNewId();

        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Cancelled, auditDecision, now);
    }

    public Task<AccessRequestDetailsResponseModel> Extend(ClaimsPrincipal user, Guid id, AccessLeaseExtensionRequestModel model)
        => throw new NotImplementedException();
}
