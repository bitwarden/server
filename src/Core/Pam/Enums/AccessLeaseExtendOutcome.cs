namespace Bit.Core.Pam.Enums;

/// <summary>
/// The result of a race-safe lease extension. The extension stored procedure returns a distinct integer code so the
/// caller can tell a lease that is no longer extendable apart from a per-lease max-extensions conflict.
/// </summary>
public enum AccessLeaseExtendOutcome
{
    /// <summary>The lease's window was extended (stored proc returned 1).</summary>
    Extended = 1,

    /// <summary>
    /// The lease was no longer active, or its window had already ended, when the guarded update ran (stored proc
    /// returned 0). A concurrent revoke or expiry likely won.
    /// </summary>
    LeaseNotActive = 0,

    /// <summary>
    /// The lease has already been extended the maximum number of times permitted by its governing rule (stored proc
    /// returned -1). Nothing was persisted.
    /// </summary>
    MaxExtensionsReached = -1,
}
