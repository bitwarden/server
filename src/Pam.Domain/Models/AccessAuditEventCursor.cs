namespace Bit.Pam.Models;

/// <summary>
/// The last event of an audit trail page, where the next page starts. <c>Id</c> breaks ties, since an action's
/// Attempt and Outcome share a timestamp.
/// </summary>
public record AccessAuditEventCursor(DateTime OccurredDate, Guid Id);
