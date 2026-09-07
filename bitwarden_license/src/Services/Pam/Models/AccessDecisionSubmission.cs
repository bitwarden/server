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
    /// The approver's note: the reason shown to the requester and carried by the audit record. Required
    /// (non-blank) on a <see cref="AccessDecisionVerdict.Deny"/> verdict, optional on an approval.
    /// </summary>
    public string? Comment { get; init; }
}
