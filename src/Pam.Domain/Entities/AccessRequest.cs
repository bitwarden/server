using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A request to lease access to a cipher in a leasing-governed collection. Auto-approved requests are created
/// with <see cref="Action"/> already <see cref="AccessRequestAction.Approved"/>; requests requiring human
/// approval are created with no action, resolved later by an approver. Neither mints the lease — the requester
/// activates the approved request within its window, which produces the <see cref="AccessLease"/>.
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

    /// <summary>
    /// The action a party has taken on the request, if any — a record of what happened, not current standing;
    /// the wire's <see cref="AccessRequestStatus"/> is derived from it against the clock via
    /// <see cref="AccessStatusDerivation.ComputeStatus"/>. Doubles as the concurrency token the transition
    /// procedures' guarded UPDATEs key off.
    /// </summary>
    public AccessRequestAction Action { get; set; }

    /// <summary>
    /// When the request was submitted, stamped in UTC at construction.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the current <see cref="Action"/> was recorded; null iff <see cref="Action"/> is
    /// <see cref="AccessRequestAction.None"/>. Overwritten on cancel-after-approval; the approval time survives
    /// in the decision row instead.
    /// </summary>
    public DateTime? ActionDate { get; set; }

    /// <summary>
    /// The access rule that governed this request, resolved once at submit (oldest wins) and pinned here so every
    /// downstream operation reads the same rule rather than re-resolving. Null for requests created before pinning
    /// existed, or when the cipher was not leasing-gated through a stored rule.
    /// </summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// Whether the request's window can still produce anything as of <paramref name="asOf"/> — an answer while open
    /// (<see cref="Action"/> None), an activation while approved. Write guards compose this with a check on
    /// <see cref="Action"/> rather than consulting the derived status enum, which never appears on the write path.
    /// </summary>
    public bool IsWindowOpen(DateTime asOf) => asOf < NotAfter;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
