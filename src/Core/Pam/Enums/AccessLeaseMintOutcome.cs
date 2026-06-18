namespace Bit.Pam.Enums;

/// <summary>
/// The result of a race-safe lease mint. The mint stored procedures return a distinct integer code so the caller can
/// tell a lost-the-race precondition failure apart from a per-cipher single-active-lease conflict.
/// </summary>
public enum AccessLeaseMintOutcome
{
    /// <summary>The active lease was minted (stored proc returned 1).</summary>
    Minted = 1,

    /// <summary>
    /// A precondition no longer held when the guarded insert ran (stored proc returned 0, or the unique-index
    /// backstop fired). A concurrent activation likely won; the caller re-reads the winner.
    /// </summary>
    PreconditionFailed = 0,

    /// <summary>
    /// Another active in-window lease already exists for this cipher and the governing rule enforces a per-cipher
    /// singleton (stored proc returned -1). Nothing was persisted.
    /// </summary>
    SingleActiveLeaseConflict = -1,
}
