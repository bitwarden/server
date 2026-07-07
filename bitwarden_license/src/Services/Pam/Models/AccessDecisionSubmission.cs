using Bit.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// An approver's decision on a pending lease request: approve or deny, with an optional comment.
/// </summary>
public sealed class AccessDecisionSubmission
{
    public required AccessDecisionVerdict Verdict { get; init; }
    public string? Comment { get; init; }
}
