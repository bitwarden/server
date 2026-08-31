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
    /// The cipher the pre-check was run for. Passed in because <see cref="AccessPreCheckResult"/> describes only the
    /// outcome and does not carry the subject cipher.
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
        // AsUtc for the reason every other PAM timestamp does it: Dapper hands back Kind.Unspecified, which
        // serializes without a designator and is then read as local time. Here that would hand the requester a retry
        // time off by their UTC offset — for anyone east of UTC, one already in the past.
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
    /// The duration, in seconds, the request form should pre-select — the governing rule's default when it sets one,
    /// otherwise the global default, clamped to <see cref="MaxDurationSeconds"/>.
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
    /// per-cipher single-active-lease constraint binds for this caller and another member holds the slot; a caller
    /// with an ungated or non-singleton path to the cipher is unconstrained and reads true regardless.
    ///
    /// A current-state hint, re-checked for real at start — the request is still worth submitting, it just cannot be
    /// activated until the slot frees. Clients that do not understand this field must treat its absence as true.
    ///
    /// Answers about <em>now</em>, and is reported for both approval modes: the singleton is re-checked at start
    /// whichever path approved the request. A caller choosing a future window should weigh it accordingly — the slot
    /// being taken now says nothing about that window.
    /// </summary>
    // Initialized true so the polarity holds on every construction path, including the parameterless
    // (de)serialization constructor — a default-constructed model must not read as blocked.
    public bool CanStartLease { get; set; } = true;

    /// <summary>
    /// When the lease currently holding the slot ends, so the requester can be given a retry time. Null whenever
    /// <see cref="CanStartLease"/> is true. Carries no holder identity by design.
    /// </summary>
    public DateTime? SlotFreesAt { get; set; }
}
