using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// Maps the backend <see cref="LeaseRequestStatus"/> (plus whether the request has produced a lease) to the status
/// vocabulary the approver-inbox client expects: <c>pending | approved | activated | denied | cancelled | expired</c>.
/// An approved request that has produced a lease is reported as <c>activated</c>.
/// </summary>
public static class InboxRequestStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Activated = "activated";
    public const string Denied = "denied";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";

    public static string From(LeaseRequestStatus status, bool hasLease) => status switch
    {
        LeaseRequestStatus.Pending => Pending,
        LeaseRequestStatus.Approved => hasLease ? Activated : Approved,
        LeaseRequestStatus.Denied => Denied,
        LeaseRequestStatus.Cancelled => Cancelled,
        LeaseRequestStatus.ExpiredUnanswered => Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
