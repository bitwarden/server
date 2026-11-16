using Bit.Services.Pam.Enums;
namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of a pre-check. <see cref="ApprovalMode"/> is the path a new request would take, unless
/// <see cref="HasActiveLease"/> sends the caller straight to the credential.
/// </summary>
/// <param name="CanStartLease">False only when another member holds the cipher's single-active-lease slot.</param>
public sealed record AccessPreCheckResult(
    AccessApprovalMode ApprovalMode,
    bool HasActiveLease = false,
    int DefaultDurationSeconds = LeaseDurationBounds.GlobalDefaultSeconds,
    int MaxDurationSeconds = LeaseDurationBounds.GlobalMaxSeconds,
    bool CanStartLease = true,
    DateTime? SlotFreesAt = null);
