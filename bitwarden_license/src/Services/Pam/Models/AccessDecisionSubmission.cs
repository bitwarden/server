using Bit.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// An approver's decision on a pending access request.
/// </summary>
public sealed class AccessDecisionSubmission
{
    public required AccessDecisionVerdict Verdict { get; init; }

    /// <summary>
    /// The approver's note, shown to the requester.
    /// </summary>
    public string? Comment { get; init; }
}
