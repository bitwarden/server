using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A request to lease access to a cipher in a leasing-governed collection. Auto-approved requests are created
/// already <see cref="AccessRequestStatus.Approved"/>; requests that require human approval are created
/// <see cref="AccessRequestStatus.Pending"/> and resolved later by an approver. Neither approval mints the lease —
/// the requester activates the approved request within its window, and that activation produces the
/// <see cref="AccessLease"/>.
/// </summary>
public class AccessRequest : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>
    /// NULL for original requests. Set only for extension requests, which point at the lease being extended.
    /// </summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>
    /// The requested access window. For automatic approval this is <c>now</c>; for human approval it is the
    /// requester-supplied start.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// The end of the requested access window. For automatic approval this is <c>now + duration</c>; for human
    /// approval it is the requester-supplied end.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Optional for automatic approval, required for human approval (enforced in the command).
    /// </summary>
    public string? Reason { get; set; }

    public AccessRequestStatus Status { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set when the request leaves <see cref="AccessRequestStatus.Pending"/>.
    /// </summary>
    public DateTime? ResolvedDate { get; set; }

    /// <summary>
    /// When the most recent activation attempt was refused (e.g. single-active-lease contention). Last-only: a refused
    /// activation leaves the request <see cref="AccessRequestStatus.Approved"/> and re-activatable, so this records only
    /// the latest refusal, for the audit trail. There is no "rejected by" — the actor is always the requester.
    /// </summary>
    public DateTime? RejectedDate { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
