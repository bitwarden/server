namespace Bit.Pam.Enums;

/// <summary>
/// The one place a derived status comes from. Nothing clock-dependent is ever stored; status is computed here at
/// read time against the same clock comparisons the stored-procedure WHERE clauses use.
/// </summary>
public static class AccessStatusDerivation
{
    /// <summary>
    /// A request's status derived from its stored <see cref="AccessRequestAction"/>. Recorded facts beat the
    /// clock (Denied/Cancelled are terminal); only the open and approved-unactivated cases consult it.
    /// </summary>
    /// <param name="hasLease">Whether the request has produced a lease; an activated request stays Approved and cannot lapse.</param>
    /// <param name="isExtension">Whether the request extends an existing lease; an applied extension stays Approved and cannot lapse.</param>
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
    /// early end beats the clock; only an untouched lease is the clock's to judge.
    /// </summary>
    /// <remarks>
    /// <c>NotBefore</c> is deliberately absent: a lease's <c>NotBefore</c> is always in the past by the time the
    /// row exists, so there is no "minted but not yet started" lease.
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
