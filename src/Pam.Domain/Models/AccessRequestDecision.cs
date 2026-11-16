using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One <see cref="Entities.AccessDecision"/> in <see cref="AccessRequestDetails.Decisions"/>, with a human approver's
/// denormalized name and email.
/// </summary>
public class AccessRequestDecision
{
    public AccessDeciderKind DeciderKind { get; set; }

    /// <summary>Null for an automatic decision.</summary>
    public Guid? ApproverId { get; set; }

    /// <summary>Null for an automatic decision or an unresolved user.</summary>
    public string? Name { get; set; }

    /// <summary>The fallback display when <see cref="Name"/> is unset.</summary>
    public string? Email { get; set; }

    /// <summary>An approver's comment, or the reason for an automatic denial.</summary>
    public string? Comment { get; set; }

    public AccessDecisionVerdict Verdict { get; set; }

    public DateTime DecidedAt { get; set; }
}
