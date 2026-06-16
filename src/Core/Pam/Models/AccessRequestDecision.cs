using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// One decision on an <see cref="Entities.AccessRequest"/>, projected from an <see cref="Entities.AccessDecision"/>
/// row. The element of <see cref="AccessRequestDetails.Decisions"/> — there is one per recorded decision, human or
/// automatic. A human decision carries the approver's identity (<see cref="Id"/> plus the denormalized name/email); an
/// automatic decision has none (<see cref="Id"/> null — it was decided by an access-rule condition).
/// </summary>
public class AccessRequestDecision
{
    /// <summary>Who decided: a human approver or an automatic condition evaluation.</summary>
    public AccessDeciderKind DeciderKind { get; set; }

    /// <summary>The human approver, or null for an automatic decision.</summary>
    public Guid? Id { get; set; }

    /// <summary>The human approver's display name, or null (automatic, or the server could not resolve the user).</summary>
    public string? Name { get; set; }

    /// <summary>The human approver's email, the fallback display when <see cref="Name"/> is unset.</summary>
    public string? Email { get; set; }

    /// <summary>The decision's comment (a human approver's note, or a future automatic-evaluation reason), if any.</summary>
    public string? Comment { get; set; }

    /// <summary>The verdict reached.</summary>
    public AccessDecisionVerdict Verdict { get; set; }

    /// <summary>When the decision was made (the decision's CreationDate).</summary>
    public DateTime DecidedAt { get; set; }
}
