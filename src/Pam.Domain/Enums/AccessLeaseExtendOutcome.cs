namespace Bit.Pam.Enums;

/// <summary>
/// The result of a race-safe lease extension. The extension stored procedure returns a distinct integer code so the
/// caller can tell a lease that is no longer extendable apart from one that has already been extended.
/// </summary>
public enum AccessLeaseExtendOutcome
{
    /// <summary>The lease's window was extended (stored proc returned 1).</summary>
    Extended = 1,

    /// <summary>
    /// The lease was inactive, or its window had already ended, when the guarded update ran.
    /// </summary>
    /// <remarks>
    /// The request is still recorded, as Denied with an automatic Deny decision naming why. Contrast
    /// <see cref="AlreadyExtended"/>, which persists nothing at all.
    /// </remarks>
    LeaseNotActive = 0,

    /// <summary>
    /// The lease has already been extended (a lease may be extended once; stored proc returned -1). Nothing was
    /// persisted.
    /// </summary>
    AlreadyExtended = -1,
}
