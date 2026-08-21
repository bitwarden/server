using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Services.Pam.Errors;

// The lease surface's failures: extending an active lease and ending one early. The Type contract is the one
// described in AccessRequestErrors.cs — stable, never localized, never reworded.
//
// Two failures here reuse a code defined for the request surface: a cipher that no longer has a governing rule
// (CipherNotGated) and a non-positive duration (DurationMustBePositive) mean the same thing whichever endpoint
// found them, so they are the same code rather than a near-duplicate a client would have to learn twice.

/// <summary>
/// The lease does not exist, or belongs to someone the caller may neither hold nor manage. One error for both, so
/// a caller cannot probe for leases they cannot touch.
/// </summary>
public record AccessLeaseNotFound() : NotFoundError();

/// <summary>
/// The lease has ended — revoked, cancelled or lapsed — so it can no longer be extended. Shares its code with
/// <see cref="AccessLeaseNotActiveForRevoke"/>: same condition, reached from a different endpoint.
/// </summary>
public record AccessLeaseNoLongerActive()
    : ConflictError("This lease is no longer active."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_lease_not_active";
}

/// <summary>
/// The lease has already ended, so there is no live access for a revoke to end. Shares its code with
/// <see cref="AccessLeaseNoLongerActive"/>.
/// </summary>
public record AccessLeaseNotActiveForRevoke()
    : ConflictError("This lease is not active."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_lease_not_active";
}

/// <summary>The cipher's governing rule does not opt in to extensions.</summary>
public record ExtensionsNotAllowed()
    : BadRequestError("This item does not allow extending a lease."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "extensions_not_allowed";
}

/// <summary>
/// The requested extension is longer than the governing rule's maximum extension length. A rule that allows
/// extensions without setting one denies every extension, and lands here.
/// </summary>
public record ExtensionExceedsMax()
    : BadRequestError("The requested duration exceeds the maximum extension length for this item."), IValidationError
{
    public string PropertyName => "durationSeconds";
    public string Type => "extension_exceeds_max";
}

/// <summary>An extension is recorded against the audit trail, so it has to say why it was taken.</summary>
public record ExtensionReasonRequired()
    : BadRequestError("A justification is required to extend a lease."), IValidationError
{
    public string PropertyName => "reason";
    public string Type => "extension_reason_required";
}

/// <summary>A lease may be extended exactly once, and this one already has been.</summary>
public record AccessLeaseAlreadyExtended()
    : BadRequestError("This lease has already been extended."), IValidationError
{
    public string PropertyName => PamErrorProperties.Code;
    public string Type => "access_lease_already_extended";
}
