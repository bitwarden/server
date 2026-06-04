using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The result of a pre-check: whether requesting access to a cipher would be approved automatically or require human
/// approval.
/// </summary>
public sealed record AccessPreCheckResult(AccessApprovalOutcome Outcome);
