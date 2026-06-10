using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// Maps the backend <see cref="AccessLeaseStatus"/> to the status vocabulary the leasing client expects:
/// <c>active | expired | revoked</c>. Mirrors <see cref="AccessRequestStatusNames"/> for the request side.
/// </summary>
public static class AccessLeaseStatusNames
{
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Revoked = "revoked";

    public static string From(AccessLeaseStatus status) => status switch
    {
        AccessLeaseStatus.Active => Active,
        AccessLeaseStatus.Expired => Expired,
        AccessLeaseStatus.Revoked => Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
