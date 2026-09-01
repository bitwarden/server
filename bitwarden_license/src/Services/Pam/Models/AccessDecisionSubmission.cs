using Bit.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// An approver's decision on a pending lease request: approve or deny, with a comment that is optional on an
/// approval and required on a denial -- see <see cref="Comment"/>.
/// </summary>
public sealed class AccessDecisionSubmission
{
    public required AccessDecisionVerdict Verdict { get; init; }

    /// <summary>
    /// The approver's note. Required (non-blank) when <see cref="Verdict"/> is
    /// <see cref="AccessDecisionVerdict.Deny"/>: it is the reason the requester is shown and the audit record
    /// carries. Optional on an approval.
    /// </summary>
    public string? Comment { get; init; }
}
