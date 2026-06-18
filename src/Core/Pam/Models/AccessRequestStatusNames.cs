using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// Maps the backend <see cref="AccessRequestStatus"/> (plus whether the request has produced a lease) to the status
/// vocabulary the approver-inbox client expects: <c>pending | approved | activated | denied | cancelled | expired</c>.
/// An approved request that has produced a lease is reported as <c>activated</c>.
/// </summary>
public static class AccessRequestStatusNames
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Activated = "activated";
    public const string Denied = "denied";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";

    public static string From(AccessRequestStatus status, bool hasLease) => status switch
    {
        AccessRequestStatus.Pending => Pending,
        AccessRequestStatus.Approved => hasLease ? Activated : Approved,
        AccessRequestStatus.Denied => Denied,
        AccessRequestStatus.Cancelled => Cancelled,
        AccessRequestStatus.ExpiredUnanswered => Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
