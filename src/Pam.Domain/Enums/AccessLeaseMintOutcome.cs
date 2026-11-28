namespace Bit.Pam.Enums;

/// <summary>The result of a race-safe lease mint; each value is the code the stored procedure returns.</summary>
public enum AccessLeaseMintOutcome
{
    Minted = 1,

    /// <summary>
    /// A precondition no longer held, or the unique-index backstop fired. A concurrent activation likely won; the
    /// caller re-reads the winner.
    /// </summary>
    PreconditionFailed = 0,

    /// <summary>The rule allows one active lease per cipher, and another is active. Nothing is persisted.</summary>
    SingleActiveLeaseConflict = -1,

    /// <summary>The requester already holds a live lease on the item, whatever the rule. Nothing is persisted.</summary>
    RequesterActiveLeaseConflict = -2,
}
