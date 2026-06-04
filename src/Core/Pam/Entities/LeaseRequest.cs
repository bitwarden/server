using Bit.Core.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Pam.Entities;

/// <summary>
/// A request to lease access to a cipher in a leasing-governed collection. Auto-approved requests are created
/// already <see cref="LeaseRequestStatus.Approved"/> alongside an active <see cref="Lease"/>; requests that require
/// human approval are created <see cref="LeaseRequestStatus.Pending"/> and resolved later by an approver.
/// </summary>
public class LeaseRequest : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>
    /// NULL for original requests. Set only for extension requests, which point at the lease being extended.
    /// </summary>
    public Guid? LeaseId { get; set; }

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

    public LeaseRequestStatus Status { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set when the request leaves <see cref="LeaseRequestStatus.Pending"/>.
    /// </summary>
    public DateTime? ResolvedDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
