using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One decision on an <see cref="AccessRequest"/>. Besides its approval or denial, a request gets a human Deny when
/// its lease is ended early or an approver retracts it.
/// </summary>
public class AccessDecision : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public AccessDeciderKind DeciderKind { get; set; }

    /// <summary>Null for an automatic decision.</summary>
    public Guid? ApproverId { get; set; }

    /// <summary>Null for a human decision, and not yet populated for automatic ones.</summary>
    public AccessConditionKind? ConditionKind { get; set; }

    public AccessDecisionVerdict Verdict { get; set; }

    /// <summary>An approver's comment, or the reason for an automatic denial.</summary>
    public string? Comment { get; set; }

    /// <summary>Always null; reserved for a snapshot of the inputs an evaluation saw.</summary>
    public string? EvaluationContext { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
