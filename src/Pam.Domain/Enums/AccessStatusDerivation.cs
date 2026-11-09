namespace Bit.Pam.Enums;

/// <summary>
/// Derives statuses at read time, since nothing clock-dependent is stored. Uses the same clock comparisons as the
/// stored-procedure WHERE clauses.
/// </summary>
public static class AccessStatusDerivation
{
    /// <summary>
    /// A request's status as of <paramref name="now"/>. Denied and Cancelled are terminal; only an open or
    /// approved-unactivated request consults the clock.
    /// </summary>
    public static AccessRequestStatus ComputeStatus(
        AccessRequestAction action, bool hasLease, bool isExtension, DateTime notAfter, DateTime now)
    {
        var windowOpen = now < notAfter;

        return action switch
        {
            AccessRequestAction.Cancelled => AccessRequestStatus.Cancelled,
            AccessRequestAction.Denied => AccessRequestStatus.Denied,

            // An applied extension did its work at creation, and an activated request continues on its lease, so
            // neither lapses.
            AccessRequestAction.Approved when isExtension || hasLease => AccessRequestStatus.Approved,

            AccessRequestAction.Approved => windowOpen ? AccessRequestStatus.Approved : AccessRequestStatus.Expired,
            AccessRequestAction.None => windowOpen ? AccessRequestStatus.Pending : AccessRequestStatus.Expired,

            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    /// <summary>
    /// A lease's status as of <paramref name="now"/>. Ignores <c>NotBefore</c>, which is always past once the lease
    /// row exists.
    /// </summary>
    public static AccessLeaseStatus ComputeLeaseStatus(AccessLeaseAction action, DateTime notAfter, DateTime now) =>
        action switch
        {
            AccessLeaseAction.Revoked => AccessLeaseStatus.Revoked,
            AccessLeaseAction.Cancelled => AccessLeaseStatus.Cancelled,
            AccessLeaseAction.None => now < notAfter ? AccessLeaseStatus.Active : AccessLeaseStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
