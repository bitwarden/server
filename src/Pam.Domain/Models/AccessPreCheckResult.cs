using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The result of a pre-check. When <see cref="HasActiveLease"/> is true the caller already holds an active lease for
/// the cipher, so the client should reveal the credential rather than prompt for a new request; otherwise
/// <see cref="ApprovalMode"/> describes whether a fresh request would be approved automatically or require human
/// approval.
/// </summary>
public sealed record AccessPreCheckResult(AccessApprovalMode ApprovalMode, bool HasActiveLease = false);
