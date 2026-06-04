using Bit.Core.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Pam.Entities;

/// <summary>
/// A single decision on a <see cref="LeaseRequest"/>. In v0 there is exactly one decision per request: an automated
/// <see cref="LeaseDecisionKind.Policy"/> verdict for auto-approval, or a <see cref="LeaseDecisionKind.Human"/>
/// verdict once approver endpoints land.
/// </summary>
public class LeaseDecision : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid LeaseRequestId { get; set; }

    public LeaseDecisionKind DeciderKind { get; set; }

    /// <summary>
    /// The human approver. NULL when <see cref="DeciderKind"/> is <see cref="LeaseDecisionKind.Policy"/>.
    /// </summary>
    public Guid? ApproverId { get; set; }

    /// <summary>
    /// The rule kind that decided (e.g. <c>ip_allowlist</c>). NULL when <see cref="DeciderKind"/> is
    /// <see cref="LeaseDecisionKind.Human"/>.
    /// </summary>
    public string? PolicyKind { get; set; }

    public LeaseDecisionVerdict Decision { get; set; }

    /// <summary>
    /// Human comment, or a future policy reason string.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Forward-compatible snapshot of the inputs a policy saw. Null in this slice (no signals are evaluated).
    /// </summary>
    public string? EvaluationContext { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
