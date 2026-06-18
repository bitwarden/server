using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A single decision on a <see cref="AccessRequest"/>. In v0 there is exactly one decision per request: an automated
/// <see cref="AccessDeciderKind.Automatic"/> verdict for auto-approval, or a <see cref="AccessDeciderKind.Human"/>
/// verdict once approver endpoints land.
/// </summary>
public class AccessDecision : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public AccessDeciderKind DeciderKind { get; set; }

    /// <summary>
    /// The human approver. NULL when <see cref="DeciderKind"/> is <see cref="AccessDeciderKind.Automatic"/>.
    /// </summary>
    public Guid? ApproverId { get; set; }

    /// <summary>
    /// The condition kind that decided (e.g. <see cref="AccessConditionKind.IpAllowlist"/>). NULL when
    /// <see cref="DeciderKind"/> is <see cref="AccessDeciderKind.Human"/>.
    /// </summary>
    public AccessConditionKind? ConditionKind { get; set; }

    public AccessDecisionVerdict Verdict { get; set; }

    /// <summary>
    /// Human comment, or a future automatic-evaluation reason string.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Forward-compatible snapshot of the inputs the evaluation saw. Null in this slice (no signals are evaluated).
    /// </summary>
    public string? EvaluationContext { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
