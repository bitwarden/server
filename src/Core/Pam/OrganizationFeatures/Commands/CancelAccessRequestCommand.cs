using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class CancelAccessRequestCommand : ICancelAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly TimeProvider _timeProvider;

    public CancelAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _timeProvider = timeProvider;
    }

    public async Task CancelAsync(Guid userId, Guid requestId)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and someone else's request, so the caller can't probe for requests they don't own.
        // Mirrors ActivateAccessRequestCommand.
        if (request is null || request.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        // Only a still-pending request can be withdrawn. A resolved request (approved/denied/cancelled/expired) is
        // terminal; surfaced as a conflict rather than a silent success so the client refreshes its view. The
        // repository write is additionally guarded on Status = Pending to stay race-safe.
        if (request.Status != AccessRequestStatus.Pending)
        {
            throw new ConflictException("This request has already been resolved.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _accessRequestRepository.CancelAsync(request.Id, now);

        // The request just left the pending queue; tell every approver of this collection to re-fetch so the
        // withdrawn request drops out of their inbox. Mirrors decide.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);
    }
}
