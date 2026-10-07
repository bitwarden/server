using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A request to lease a cipher in a leasing-governed collection, created Approved on the automatic path or open for
/// an approver. The requester activates an approved request to mint its <see cref="AccessLease"/>.
/// </summary>
public class AccessRequest : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>The lease an extension request extends; null otherwise.</summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>
    /// The submission time on the automatic path, the requester's chosen start on the human path, or the lease's
    /// current end for an extension.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// <c>NotBefore</c> plus the requested duration, or the requester's chosen end on the human path.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>Required on the human and extension paths, optional on the automatic one.</summary>
    public string? Reason { get; set; }

    /// <summary>Doubles as the concurrency token the transition procedures' guarded UPDATEs key off.</summary>
    public AccessRequestAction Action { get; set; }

    /// <summary>When the request was submitted.</summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the current <see cref="Action"/> was recorded. A cancel or retraction after approval overwrites it; the
    /// approval time survives on its decision row.
    /// </summary>
    public DateTime? ActionDate { get; set; }

    /// <summary>
    /// The governing rule, resolved once at submit (oldest wins) so later operations read the same rule. Null once the
    /// rule is deleted, or when no stored rule gated the cipher.
    /// </summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// Whether the window still allows an answer or an activation. Write guards pair this with an
    /// <see cref="Action"/> check rather than the derived status, which never appears on the write path.
    /// </summary>
    public bool IsWindowOpen(DateTime asOf) => asOf < NotAfter;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
