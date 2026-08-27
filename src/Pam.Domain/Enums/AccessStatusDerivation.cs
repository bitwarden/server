namespace Bit.Pam.Enums;

/// <summary>
/// The one place a derived status comes from. The database stores facts — what a party did to a record, and when;
/// what a record <em>means right now</em> is an interpretation of those facts against the clock, computed here at
/// read time. Nothing clock-dependent is ever stored, so nothing stored can go stale. The stored-procedure WHERE
/// clauses carry the same plain clock comparisons where filtering requires them; the two must not drift.
/// </summary>
public static class AccessStatusDerivation
{
    /// <summary>
    /// A request's status as of <paramref name="now"/>, derived from its stored <see cref="AccessRequestAction"/>.
    /// Recorded facts beat the clock (Denied/Cancelled are terminal whatever the window says); only the open and
    /// approved-unactivated cases consult it, and Expired exists nowhere else.
    /// </summary>
    /// <param name="hasLease">Whether the request has produced a lease. An activated request's story continues on
    /// its lease, so it stays Approved and cannot lapse.</param>
    /// <param name="isExtension">Whether the request extends an existing lease. An applied extension finished its
    /// work at creation (the parent lease's end moved in place), so it stays Approved and cannot lapse.</param>
    public static AccessRequestStatus ComputeStatus(
        AccessRequestAction action, bool hasLease, bool isExtension, DateTime notAfter, DateTime now)
    {
        var windowOpen = now < notAfter;

        return action switch
        {
            AccessRequestAction.Cancelled => AccessRequestStatus.Cancelled,
            AccessRequestAction.Denied => AccessRequestStatus.Denied,

            // An applied extension finished its work at creation; an activated request's story continues on its
            // lease. Neither can lapse back out of Approved.
            AccessRequestAction.Approved when isExtension || hasLease => AccessRequestStatus.Approved,

            // Only the clock separates the remaining cases: an unactivated approval is usable while its window is
            // open, an open request is answerable while its window is open.
            AccessRequestAction.Approved => windowOpen ? AccessRequestStatus.Approved : AccessRequestStatus.Expired,
            AccessRequestAction.None => windowOpen ? AccessRequestStatus.Pending : AccessRequestStatus.Expired,

            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    /// <summary>
    /// A lease's status as of <paramref name="now"/>, derived from its stored <see cref="AccessLeaseAction"/>. An
    /// early end beats the clock — ended early ended early, whatever <paramref name="notAfter"/> says; only an
    /// untouched lease is the clock's to judge.
    /// </summary>
    /// <remarks>
    /// <c>NotBefore</c> is deliberately absent (load-bearing invariant): a lease's <c>NotBefore</c> is in the past
    /// from the moment the row exists — activation rejects a future start and the mint procedure re-guards it — so
    /// there is no "minted but not yet started" lease. Authorization is stricter than display and checks both window
    /// ends in its reads; those checks are vacuous by this invariant and correct by construction. If scheduled
    /// requests ever grow auto-activation at window start, pre-start leases begin to exist and the derived enum
    /// needs a new value (scheduled, from action None with a future start) — never fix that by storing a status.
    /// </remarks>
    public static AccessLeaseStatus ComputeLeaseStatus(AccessLeaseAction action, DateTime notAfter, DateTime now) =>
        action switch
        {
            AccessLeaseAction.Revoked => AccessLeaseStatus.Revoked,
            AccessLeaseAction.Cancelled => AccessLeaseStatus.Cancelled,
            AccessLeaseAction.None => now < notAfter ? AccessLeaseStatus.Active : AccessLeaseStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
