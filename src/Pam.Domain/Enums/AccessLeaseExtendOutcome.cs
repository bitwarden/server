namespace Bit.Pam.Enums;

/// <summary>The result of a race-safe lease extension; each value is the code the stored procedure returns.</summary>
public enum AccessLeaseExtendOutcome
{
    Extended = 1,

    /// <summary>
    /// The lease had ended or was ended early. The request is still recorded, as Denied with an automatic Deny
    /// decision.
    /// </summary>
    LeaseNotActive = 0,

    /// <summary>A lease may be extended once. Nothing is persisted.</summary>
    AlreadyExtended = -1,
}
