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
public sealed record AccessPreCheckResult(
    AccessApprovalMode ApprovalMode,
    bool HasActiveLease = false,
    int DefaultDurationSeconds = LeaseDurationBounds.GlobalDefaultSeconds,
    int MaxDurationSeconds = LeaseDurationBounds.GlobalMaxSeconds);
