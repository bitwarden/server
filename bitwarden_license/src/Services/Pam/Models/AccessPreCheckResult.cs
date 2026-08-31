using Bit.Services.Pam.Enums;
namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of a pre-check. When <see cref="HasActiveLease"/> is true the caller already holds an active lease for
/// the cipher, so the client should reveal the credential rather than prompt for a new request; otherwise
/// <see cref="ApprovalMode"/> describes whether a fresh request would be approved automatically or require human
/// approval.
/// </summary>
/// <param name="ApprovalMode">The approval path a fresh request would take.</param>
/// <param name="HasActiveLease">True when the caller already holds an active lease for the cipher.</param>
/// <param name="DefaultDurationSeconds">
/// The duration a request form should pre-select, already resolved against the governing rule and clamped to
/// <paramref name="MaxDurationSeconds"/>. Never null: a rule storing no default of its own falls back to the global
/// one, so the client needs no fallback of its own.
/// </param>
/// <param name="MaxDurationSeconds">
/// The longest duration (automatic path) or window span (human path) the server will accept for this cipher — the
/// governing rule's cap narrowed by the global ceiling. Published so the client can offer only durations that will be
/// accepted, instead of letting the requester pick one that submit then rejects.
/// </param>
/// <param name="CanStartLease">
/// Whether a lease could be started right now — the spec's <c>RuleAllowsLease</c>. False only when the per-cipher
/// single-active-lease constraint binds for this caller <em>and</em> another member's lease is currently active on the
/// cipher; a caller with an ungated or non-singleton path is unconstrained and reads true regardless. A current-state
/// hint, re-checked for real at start: the mint procedure's range lock is the actual gate. Defaults true so absence
/// never reads as blocked.
/// </param>
/// <param name="SlotFreesAt">
/// When the lease blocking <paramref name="CanStartLease"/> ends, so the requester gets a retry time instead of
/// polling. The <em>latest</em> end among any concurrent leases — the slot frees when the last one does. Null whenever
/// <paramref name="CanStartLease"/> is true. Carries no holder identity by design (PM-42446, Alternative A).
/// </param>
public sealed record AccessPreCheckResult(
    AccessApprovalMode ApprovalMode,
    bool HasActiveLease = false,
    int DefaultDurationSeconds = LeaseDurationBounds.GlobalDefaultSeconds,
    int MaxDurationSeconds = LeaseDurationBounds.GlobalMaxSeconds,
    bool CanStartLease = true,
    DateTime? SlotFreesAt = null);
