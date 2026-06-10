using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class RevokeAccessLeaseCommand : IRevokeAccessLeaseCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly TimeProvider _timeProvider;

    public RevokeAccessLeaseCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _timeProvider = timeProvider;
    }

    public async Task RevokeAsync(Guid userId, Guid leaseId, string? reason)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(leaseId);

        // 404 for both missing and not-visible, so the caller can't probe for leases they don't manage.
        if (lease is null || !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, lease.CollectionId))
        {
            throw new NotFoundException();
        }

        if (lease.Status != AccessLeaseStatus.Active)
        {
            throw new ConflictException("This lease is not active.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // The reason has no dedicated column, so it is preserved as a human decision against the originating request.
        var auditDecision = new AccessDecision
        {
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = userId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = string.IsNullOrWhiteSpace(reason) ? null : reason,
            CreationDate = now,
        };
        auditDecision.SetNewId();

        await _accessLeaseRepository.RevokeAsync(lease, auditDecision, now);

        // The active lease just drained; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
    }
}
