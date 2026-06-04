using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class RequestAccessCommand : IRequestAccessCommand
{
    /// <summary>
    /// The maximum lease window length, applied to both the automatic duration and the human-requested window.
    /// Global for v0; per-rule configuration is a later concern.
    /// </summary>
    public const int MaxDurationSeconds = 24 * 60 * 60;

    private readonly ICipherRepository _cipherRepository;
    private readonly IAccessApprovalResolver _resolver;
    private readonly ILeaseRepository _leaseRepository;
    private readonly ILeaseRequestRepository _leaseRequestRepository;
    private readonly TimeProvider _timeProvider;

    public RequestAccessCommand(
        ICipherRepository cipherRepository,
        IAccessApprovalResolver resolver,
        ILeaseRepository leaseRepository,
        ILeaseRequestRepository leaseRequestRepository,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _leaseRepository = leaseRepository;
        _leaseRequestRepository = leaseRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestResult> RequestAccessAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission)
    {
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var resolution = await _resolver.ResolveAsync(userId, cipherId);
        if (resolution is null)
        {
            throw new BadRequestException("This item does not require a lease.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (await _leaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have active access to this item.");
        }

        if (await _leaseRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId) is not null)
        {
            throw new BadRequestException("You already have a pending request for this item.");
        }

        return resolution.RequiresHumanApproval
            ? await RequestHumanApprovalAsync(userId, cipherId, resolution, submission)
            : await IssueAutomaticLeaseAsync(userId, cipherId, resolution, submission, now);
    }

    private async Task<AccessRequestResult> IssueAutomaticLeaseAsync(
        Guid userId, Guid cipherId, AccessApprovalResolution resolution, AccessRequestSubmission submission, DateTime now)
    {
        if (submission.Start.HasValue || submission.End.HasValue)
        {
            throw new BadRequestException("This item is approved automatically; provide a duration, not a window.");
        }

        if (submission.DurationSeconds is not { } durationSeconds || durationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        if (durationSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested duration exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        var notAfter = now.AddSeconds(durationSeconds);

        var request = new LeaseRequest
        {
            OrganizationId = resolution.OrganizationId,
            CollectionId = resolution.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            NotBefore = now,
            NotAfter = notAfter,
            Reason = string.IsNullOrWhiteSpace(submission.Reason) ? null : submission.Reason,
            Status = LeaseRequestStatus.Approved,
            CreationDate = now,
            ResolvedDate = now,
        };
        request.SetNewId();

        var decision = new LeaseDecision
        {
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Policy,
            Decision = LeaseDecisionVerdict.Approve,
            CreationDate = now,
        };
        decision.SetNewId();

        var lease = new Lease
        {
            LeaseRequestId = request.Id,
            OrganizationId = resolution.OrganizationId,
            CollectionId = resolution.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            Status = LeaseStatus.Active,
            NotBefore = now,
            NotAfter = notAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        await _leaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        return AccessRequestResult.Automatic(lease);
    }

    private async Task<AccessRequestResult> RequestHumanApprovalAsync(
        Guid userId, Guid cipherId, AccessApprovalResolution resolution, AccessRequestSubmission submission)
    {
        if (submission.DurationSeconds.HasValue)
        {
            throw new BadRequestException("This item requires human approval; provide a start and end date, not a duration.");
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            throw new BadRequestException("A reason is required for items that need human approval.");
        }

        if (submission.Start is not { } start || submission.End is not { } end)
        {
            throw new BadRequestException("A start and end date are required.");
        }

        if (start >= end)
        {
            throw new BadRequestException("The start date must be before the end date.");
        }

        if ((end - start).TotalSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested window exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new LeaseRequest
        {
            OrganizationId = resolution.OrganizationId,
            CollectionId = resolution.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            NotBefore = start,
            NotAfter = end,
            Reason = submission.Reason,
            Status = LeaseRequestStatus.Pending,
            CreationDate = now,
        };

        var created = await _leaseRequestRepository.CreateAsync(request);
        return AccessRequestResult.Human(created);
    }
}
