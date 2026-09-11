using Bit.Services.Pam.Enums;
namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of a pre-check. If <see cref="HasActiveLease"/> is true the client should reveal the credential
/// rather than prompt for a new request; otherwise <see cref="ApprovalMode"/> describes the path a fresh request
/// would take.
/// </summary>
/// <param name="ApprovalMode">The approval path a fresh request would take.</param>
/// <param name="HasActiveLease">True if the caller already holds an active lease for the cipher.</param>
/// <param name="DefaultDurationSeconds">
/// The duration a request form should pre-select, clamped to <paramref name="MaxDurationSeconds"/>. Never null.
/// </param>
/// <param name="MaxDurationSeconds">The longest duration or window span the server will accept for this cipher.</param>
/// <param name="CanStartLease">
/// False only when the per-cipher single-active-lease constraint binds and another member's lease is active. A
/// hint, re-checked for real at start by the mint procedure's range lock.
/// </param>
/// <param name="SlotFreesAt">
/// When the blocking lease ends. Null whenever <paramref name="CanStartLease"/> is true. Carries no holder
/// identity by design.
/// </param>
public sealed record AccessPreCheckResult(
    AccessApprovalMode ApprovalMode,
    bool HasActiveLease = false,
    int DefaultDurationSeconds = LeaseDurationBounds.GlobalDefaultSeconds,
    int MaxDurationSeconds = LeaseDurationBounds.GlobalMaxSeconds,
    bool CanStartLease = true,
    DateTime? SlotFreesAt = null);
