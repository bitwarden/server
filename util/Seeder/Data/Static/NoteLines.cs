namespace Bit.Seeder.Data.Static;

/// <summary>
/// The sentences that generated login notes are built from.
/// </summary>
internal static class NoteLines
{
    /// <summary>
    /// Sentences a note repeats until it reaches its target length. A <c>{team}</c> placeholder is replaced with the
    /// team that owns the login.
    /// </summary>
    internal static readonly string[] All =
    [
        "Shared account owned by the {team} team. Ask before changing the password.",
        "Renewal is handled by procurement; contract number is in the vendor folder.",
        "MFA device is the team phone kept in the IT cabinet.",
        "Use the SSO button first; this local login is the break-glass fallback.",
        "Rotated after the last access review. Next review is due at quarter end.",
        "Support contact: vendor portal, priority 2. Account manager changes yearly.",
        "Do not use for production changes without a ticket.",
        "Recovery codes were printed and stored with the facilities safe.",
        "Read-only reporting user. Requests for write access go through the {team} lead.",
        "Billing alerts go to the shared mailbox, not to individuals.",
        "Legacy integration still points here; migration is tracked in the backlog.",
        "Security questions are set to random values stored in the custom fields."
    ];
}
