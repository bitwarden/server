using Bit.HttpExtensions;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// The resolved approval outcome for a cipher, without submitting a request — lets the client present the right
/// workflow (pick a duration vs. pick a window and justify) before the requester commits.
/// </summary>
public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel()
        : base("accessPreCheck")
    {
    }

    /// <param name="cipherId">
    /// The cipher the pre-check was run for; <see cref="AccessPreCheckResult"/> carries only the outcome.
    /// </param>
    /// <param name="result">The resolved approval outcome.</param>
    public AccessPreCheckResponseModel(Guid cipherId, AccessPreCheckResult result)
        : base("accessPreCheck")
    {
        ArgumentNullException.ThrowIfNull(result);

        CipherId = cipherId;
        ApprovalMode = result.ApprovalMode;
        HasActiveLease = result.HasActiveLease;
        DefaultDurationSeconds = result.DefaultDurationSeconds;
        MaxDurationSeconds = result.MaxDurationSeconds;
        CanStartLease = result.CanStartLease;
        // AsUtc like every other PAM timestamp: Dapper hands back Kind.Unspecified, which reads as local time.
        SlotFreesAt = result.SlotFreesAt.AsUtc();
    }

    public Guid CipherId { get; set; }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when a request would be approved immediately,
    /// <see cref="AccessApprovalMode.Human"/> when it needs an approver.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>
    /// True when the caller already holds an active lease: reveal the credential, no request needed.
    /// </summary>
    public bool HasActiveLease { get; set; }

    /// <summary>
    /// The duration, in seconds, the request form should pre-select: the governing rule's default, or the global
    /// default, clamped to <see cref="MaxDurationSeconds"/>.
    /// </summary>
    public int DefaultDurationSeconds { get; set; }

    /// <summary>
    /// The longest duration (automatic path) or window span (human path), in seconds, that a request for this cipher
    /// may ask for: the governing rule's cap narrowed by the global ceiling. Clients should offer nothing above it —
    /// submit enforces the same number.
    /// </summary>
    public int MaxDurationSeconds { get; set; }

    /// <summary>
    /// Whether access could be started right now, implementing the spec's <c>RuleAllowsLease</c>. False only when the
    /// per-cipher single-active-lease constraint binds for this caller and another member holds the slot.
    ///
    /// A current-state hint, re-checked for real at start; clients that don't understand this field must treat its
    /// absence as true. Answers about <em>now</em> only — a future window is re-checked at start regardless.
    /// </summary>
    // Defaults true so a default-constructed model, e.g. via deserialization, does not read as blocked.
    public bool CanStartLease { get; set; } = true;

    /// <summary>
    /// When the lease holding the slot ends, for a retry time. Null when <see cref="CanStartLease"/> is true.
    /// Carries no holder identity by design.
    /// </summary>
    public DateTime? SlotFreesAt { get; set; }
}
