using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// Maps the backend <see cref="LeaseStatus"/> to the status vocabulary the leasing client expects:
/// <c>active | expired | revoked</c>. Mirrors <see cref="InboxRequestStatus"/> for the request side.
/// </summary>
public static class LeaseStatusName
{
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Revoked = "revoked";

    public static string From(LeaseStatus status) => status switch
    {
        LeaseStatus.Active => Active,
        LeaseStatus.Expired => Expired,
        LeaseStatus.Revoked => Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
