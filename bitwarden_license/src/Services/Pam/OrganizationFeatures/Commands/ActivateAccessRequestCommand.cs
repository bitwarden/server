using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

/// <inheritdoc cref="IActivateAccessRequestCommand" />
public class ActivateAccessRequestCommand : IActivateAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly TimeProvider _timeProvider;

    public ActivateAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AccessLease> ActivateAsync(Guid userId, Guid requestId)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and someone else's request, so the caller can't probe for requests they don't own.
        if (request is null || request.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Activation is idempotent while the produced lease is live (double-click, a second tab racing the
        // auto-activating open flow); a revoked or lapsed lease is final — a request authorizes access at most once.
        var existing = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        if (existing is not null)
        {
            if (existing.Status == AccessLeaseStatus.Active && existing.NotAfter > now)
            {
                return existing;
            }

            throw new ConflictException("This request's access has already been used and is no longer active.");
        }

        if (request.Status != AccessRequestStatus.Approved)
        {
            throw new ConflictException(request.Status == AccessRequestStatus.Pending
                ? "This request has not been approved yet."
                : "This request can no longer be activated.");
        }

        if (request.NotBefore > now)
        {
            throw new BadRequestException("The approved access window has not started yet.");
        }

        if (request.NotAfter <= now)
        {
            throw new BadRequestException("The approved access window has already ended.");
        }

        var lease = new AccessLease
        {
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        // The per-cipher single-active-lease guard (the POC's ISingleActiveLeaseEvaluator) is deferred with the
        // governance slice, so this cut mints without enforcing it. The mint proc still runs under its range lock.
        var outcome = await _accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, enforceSingleActiveLease: false);

        if (outcome == AccessLeaseMintOutcome.SingleActiveLeaseConflict)
        {
            throw new ConflictException("Another active lease exists for this item. Try again once it ends.");
        }

        if (outcome == AccessLeaseMintOutcome.PreconditionFailed)
        {
            // Lost a race: the guarded insert re-checks every precondition, so a miss means another activation won or
            // the request changed underneath us. If the winner's lease is live, activation still succeeded for us.
            var winner = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
            if (winner is { Status: AccessLeaseStatus.Active } && winner.NotAfter > now)
            {
                return winner;
            }

            throw new ConflictException("This request can no longer be activated.");
        }

        return lease;
    }
}
