using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// An <see cref="Entities.AccessRequest"/> as reads return it: derived statuses, the produced lease, the decision log,
/// and the requester's denormalized identity.
/// </summary>
public class AccessRequestDetails
{
    public Guid Id { get; set; }

    public Guid? ExtensionOfLeaseId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    public Guid? RuleId { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.NotBefore"/>
    public DateTime NotBefore { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.NotAfter"/>
    public DateTime NotAfter { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.Reason"/>
    public string? Reason { get; set; }

    /// <summary>
    /// Derived against the read's clock and stamped at the repository boundary; the stored action is never exposed
    /// on a read model.
    /// </summary>
    public AccessRequestStatus Status { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.CreationDate"/>
    public DateTime CreationDate { get; set; }

    /// <summary>Null while no action is recorded, including on derived-Expired rows.</summary>
    public DateTime? ActionDate { get; set; }

    public Guid? ProducedLeaseId { get; set; }

    /// <summary>
    /// Derived from the lease's own action and <c>NotAfter</c>, which an extension pushes out, so the inbox offers
    /// revocation only for a live lease.
    /// </summary>
    public AccessLeaseStatus? ProducedLeaseStatus { get; set; }

    /// <summary>
    /// Differs from <see cref="NotAfter"/> once an extension pushes the lease's end out, since the originating
    /// request is never restamped.
    /// </summary>
    public DateTime? ProducedLeaseNotAfter { get; set; }

    /// <summary>Oldest first. A requester cancellation and an expiry record no decision of their own.</summary>
    public List<AccessRequestDecision> Decisions { get; set; } = new();

    /// <summary>Null when unset or the user could not be resolved.</summary>
    public string? RequesterName { get; set; }

    /// <summary>The fallback display when <see cref="RequesterName"/> is unset.</summary>
    public string? RequesterEmail { get; set; }

    /// <summary>
    /// Projects a request the caller just wrote, deriving <see cref="Status"/> as the repository reads do. The lease
    /// fields stay null, since no produced lease can exist at these call sites.
    /// </summary>
    public static AccessRequestDetails From(Entities.AccessRequest request, DateTime now)
    {
        var details = new AccessRequestDetails
        {
            Id = request.Id,
            ExtensionOfLeaseId = request.ExtensionOfLeaseId,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            RuleId = request.RuleId,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            Reason = request.Reason,
            CreationDate = request.CreationDate,
            ActionDate = request.ActionDate,
        };
        details.StampDerivedStatuses(request.Action, producedLease: null, now);
        return details;
    }

    /// <summary><see cref="NotAfter"/> and <see cref="ExtensionOfLeaseId"/> must already be set.</summary>
    public void StampDerivedStatuses(AccessRequestAction action,
        (Guid Id, AccessLeaseAction Action, DateTime NotAfter)? producedLease, DateTime now)
    {
        Status = AccessStatusDerivation.ComputeStatus(
            action, hasLease: producedLease is not null, isExtension: ExtensionOfLeaseId is not null,
            NotAfter, now);
        ProducedLeaseId = producedLease?.Id;
        ProducedLeaseNotAfter = producedLease?.NotAfter;
        ProducedLeaseStatus = producedLease is { } lease
            ? AccessStatusDerivation.ComputeLeaseStatus(lease.Action, lease.NotAfter, now)
            : null;
    }
}
