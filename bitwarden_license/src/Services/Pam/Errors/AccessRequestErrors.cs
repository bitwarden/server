using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Services.Pam.Errors;

// The access-request surface's failures: submitting a request, activating an approved one, cancelling one, and
// deciding someone else's. Every one of them carries a Type — a stable, machine-readable code the API layer renders
// into the problem response so a client can tell them apart without matching the message.
//
// The contract on a Type: it is never localized and never reworded once shipped. Reword the Message freely; that is
// display copy, and a client that shows it is showing the server's words on purpose. Retiring a code is a breaking
// change. Adding one is not — a client that does not recognize a code treats it as a generic failure.

/// <summary>
/// The requester already holds a live lease on this cipher. Not a failure the requester caused: the state they
/// asked for already exists, so a client reconciles (re-read the access state) rather than reporting an error.
/// </summary>
public record AccessAlreadyActive()
    : ConflictError("You already have active access to this item."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_already_active";
}

/// <summary>
/// The requester already has a request awaiting a decision on this cipher. Reconcile, as with
/// <see cref="AccessAlreadyActive"/>.
/// </summary>
public record AccessRequestAlreadyPending()
    : ConflictError("You already have a pending request for this item."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_already_pending";
}

/// <summary>
/// The requester already has an approved request on this cipher, waiting to be activated. Reconcile, as with
/// <see cref="AccessAlreadyActive"/>.
/// </summary>
public record AccessRequestAlreadyApproved()
    : ConflictError("You already have an approved request for this item."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_already_approved";
}

/// <summary>
/// No access rule governs the cipher, so there is nothing to lease. Also raised when extending: an extension
/// reuses the cipher's governing rule, so a cipher that no longer has one cannot be extended either.
/// </summary>
public record CipherNotGated()
    : BadRequestError("This item does not require a lease."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "cipher_not_gated";
}

/// <summary>
/// The cipher's rule approves automatically, which pins the window at submit — so the request must carry a
/// duration, and the start/end it carried instead cannot be honored.
/// </summary>
public record DurationExpected()
    : BadRequestError("This item is approved automatically; provide a duration, not a window."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "duration_expected";
}

/// <summary>
/// The cipher's rule needs a human decision, which is taken against a specific window — so the request must carry
/// a start and end, not the duration it carried.
/// </summary>
public record WindowExpected()
    : BadRequestError("This item requires human approval; provide a start and end date, not a duration."),
        IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "window_expected";
}

/// <summary>A duration was expected but was absent, zero or negative.</summary>
public record DurationMustBePositive()
    : BadRequestError("A positive duration is required."), IValidationError
{
    public string PropertyName => "durationSeconds";
    public string Type => "duration_must_be_positive";
}

/// <summary>The requested duration is longer than the governing rule allows, narrowed by the global ceiling.</summary>
public record DurationExceedsMax(int MaxDurationSeconds)
    : BadRequestError($"The requested duration exceeds the maximum of {MaxDurationSeconds} seconds."),
        IValidationError
{
    public string PropertyName => "durationSeconds";
    public string Type => "duration_exceeds_max";
}

/// <summary>A window was expected but one or both of its ends was absent.</summary>
public record WindowRequired()
    : BadRequestError("A start and end date are required."), IValidationError
{
    public string PropertyName => "start";
    public string Type => "window_required";
}

/// <summary>The requested window ends at or before it starts.</summary>
public record WindowEndBeforeStart()
    : BadRequestError("The start date must be before the end date."), IValidationError
{
    public string PropertyName => "end";
    public string Type => "window_end_before_start";
}

/// <summary>The requested window is longer than the governing rule allows, narrowed by the global ceiling.</summary>
public record WindowExceedsMax(int MaxDurationSeconds)
    : BadRequestError($"The requested window exceeds the maximum of {MaxDurationSeconds} seconds."), IValidationError
{
    public string PropertyName => "end";
    public string Type => "window_exceeds_max";
}

/// <summary>The cipher's rule needs a human decision, and an approver cannot decide without a stated reason.</summary>
public record ReasonRequired()
    : BadRequestError("A reason is required for items that need human approval."), IValidationError
{
    public string PropertyName => "reason";
    public string Type => "reason_required";
}

/// <summary>
/// The governing rule's IP allowlist does not cover the caller's address. A denial, not bad input: the same
/// request from an allowed network would succeed.
/// </summary>
public record AccessDeniedByNetwork()
    : BadRequestError("Access to this item is not permitted from your current network."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_denied_by_network";
}

/// <summary>
/// The governing rule's time window does not cover now. A denial, not bad input: the same request inside the
/// window would succeed.
/// </summary>
public record AccessDeniedBySchedule()
    : BadRequestError("Access to this item is not permitted at this time."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_denied_by_schedule";
}

/// <summary>The governing rule denied the request for a reason this server version does not name specifically.</summary>
public record AccessDenied()
    : BadRequestError("Access to this item is not permitted right now."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_denied";
}

/// <summary>
/// The cipher does not exist, or the caller cannot see it. One error for both, so a caller cannot probe the vault
/// for ciphers they have no access to.
/// </summary>
public record CipherNotFound() : NotFoundError();

/// <summary>
/// The request does not exist, or belongs to someone the caller may not see it on behalf of. One error for both,
/// so a caller cannot probe for requests they do not own.
/// </summary>
public record AccessRequestNotFound() : NotFoundError();

/// <summary>
/// The request already minted a lease and that lease has ended. A request authorizes access at most once, so
/// there is nothing left to activate.
/// </summary>
public record AccessLeaseAlreadyUsed()
    : ConflictError("This request's access has already been used and is no longer active."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_lease_already_used";
}

/// <summary>The request is still waiting on an approver, so there is no approval to activate yet.</summary>
public record AccessRequestNotApproved()
    : ConflictError("This request has not been approved yet."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_not_approved";
}

/// <summary>
/// The request has settled into a state activation cannot start from — denied, cancelled or expired — or lost the
/// race to another activation that has since ended.
/// </summary>
public record AccessRequestNotActivatable()
    : ConflictError("This request can no longer be activated."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_not_activatable";
}

/// <summary>The approved window has not opened yet, so the lease it would mint could not be used.</summary>
public record ApprovedWindowNotStarted()
    : BadRequestError("The approved access window has not started yet."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "approved_window_not_started";
}

/// <summary>The approved window has closed, so the lease it would mint would already be dead.</summary>
public record ApprovedWindowEnded()
    : BadRequestError("The approved access window has already ended."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "approved_window_ended";
}

/// <summary>
/// The cipher's rule permits one active lease at a time and someone else holds it. Transient — the same
/// activation succeeds once that lease ends.
/// </summary>
public record SingleActiveLeaseConflict()
    : ConflictError("Another active lease exists for this item. Try again once it ends."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "single_active_lease_conflict";
}

/// <summary>
/// The request has already been decided, cancelled or expired. Raised by both cancel and decide: whichever of
/// them arrives second finds the request settled.
/// </summary>
public record AccessRequestAlreadyResolved()
    : ConflictError("This request has already been resolved."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_already_resolved";
}

/// <summary>
/// The request has minted a live lease, which now governs the access — ending it is a lease revoke, not a
/// request cancel.
/// </summary>
public record AccessRequestHasActiveLease()
    : ConflictError("This request has an active lease; revoke the lease instead."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_request_has_active_lease";
}

/// <summary>
/// The caller is the request's own requester. Refused as a 400 rather than a 403 because Bitwarden clients treat
/// a 403 as a forced logout.
/// </summary>
public record CannotDecideOwnRequest()
    : BadRequestError("You cannot decide your own request."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "cannot_decide_own_request";
}

/// <summary>
/// The window the requester asked for has already closed, so approving it would mint an approval that can never
/// be activated. Denying such a request is still allowed.
/// </summary>
public record RequestedWindowEnded()
    : BadRequestError("The requested access window has already ended."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "requested_window_ended";
}
