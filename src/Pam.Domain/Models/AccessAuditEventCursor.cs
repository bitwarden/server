namespace Bit.Pam.Models;

/// <summary>
/// A keyset position in the audit trail: the last event of a page, used to fetch the page after it. Both halves are
/// needed because OccurredDate is not unique, since an action's Attempt and Outcome share a timestamp.
/// </summary>
public record AccessAuditEventCursor(DateTime OccurredDate, Guid Id);
