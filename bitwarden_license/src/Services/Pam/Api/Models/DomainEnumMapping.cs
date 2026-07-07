using Domain = Bit.Pam.Enums;

namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// Maps the PAM domain enums onto their wire-contract counterparts. The API enums are standalone copies so the DTOs
/// carry the wire contract without coupling to the PAM domain, and their values differ from the domain's: the wire
/// request status has an <c>Activated</c> member derived from whether the request has produced a lease, which shifts
/// the values after <c>Approved</c>.
/// </summary>
public static class DomainEnumMapping
{
    /// <summary>
    /// Maps the domain request status (plus whether the request has produced a lease) to the wire status. An approved
    /// request that has produced a lease is reported as <see cref="AccessRequestStatus.Activated"/>.
    /// </summary>
    public static AccessRequestStatus ToApiStatus(this Domain.AccessRequestStatus status, bool hasLease) => status switch
    {
        Domain.AccessRequestStatus.Pending => AccessRequestStatus.Pending,
        Domain.AccessRequestStatus.Approved => hasLease ? AccessRequestStatus.Activated : AccessRequestStatus.Approved,
        Domain.AccessRequestStatus.Denied => AccessRequestStatus.Denied,
        Domain.AccessRequestStatus.Cancelled => AccessRequestStatus.Canceled,
        Domain.AccessRequestStatus.ExpiredUnanswered => AccessRequestStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static AccessLeaseStatus ToApiStatus(this Domain.AccessLeaseStatus status) => status switch
    {
        Domain.AccessLeaseStatus.Active => AccessLeaseStatus.Active,
        Domain.AccessLeaseStatus.Expired => AccessLeaseStatus.Expired,
        Domain.AccessLeaseStatus.Revoked => AccessLeaseStatus.Revoked,
        Domain.AccessLeaseStatus.Cancelled => AccessLeaseStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DeciderKind ToApiKind(this Domain.AccessDeciderKind kind) => kind switch
    {
        Domain.AccessDeciderKind.Automatic => DeciderKind.Automatic,
        Domain.AccessDeciderKind.Human => DeciderKind.Human,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static AccessDecisionVerdict ToApiVerdict(this Domain.AccessDecisionVerdict verdict) => verdict switch
    {
        Domain.AccessDecisionVerdict.Deny => AccessDecisionVerdict.Deny,
        Domain.AccessDecisionVerdict.Approve => AccessDecisionVerdict.Approve,
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null),
    };

    public static Domain.AccessDecisionVerdict ToDomainVerdict(this AccessDecisionVerdict verdict) => verdict switch
    {
        AccessDecisionVerdict.Deny => Domain.AccessDecisionVerdict.Deny,
        AccessDecisionVerdict.Approve => Domain.AccessDecisionVerdict.Approve,
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null),
    };
}
