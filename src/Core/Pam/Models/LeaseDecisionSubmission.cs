using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// An approver's decision on a pending lease request: approve or deny, with an optional comment.
/// </summary>
public sealed class LeaseDecisionSubmission
{
    public required LeaseDecisionVerdict Verdict { get; init; }
    public string? Comment { get; init; }
}
