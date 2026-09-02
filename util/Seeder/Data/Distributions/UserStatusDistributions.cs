using Bit.Core.Enums;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured user status distributions for seeding scenarios.
/// </summary>
public static class UserStatusDistributions
{
    /// <summary>
    /// Realistic organization membership distribution.
    /// 85% Confirmed, 5% Invited, 5% Accepted, 5% Revoked
    /// </summary>
    public static Distribution<OrganizationUserStatusType> Realistic { get; } = new(
        (OrganizationUserStatusType.Confirmed, 0.85),
        (OrganizationUserStatusType.Invited, 0.05),
        (OrganizationUserStatusType.Accepted, 0.05),
        (OrganizationUserStatusType.Revoked, 0.05)
    );

    /// <summary>
    /// All users confirmed - for simpler testing scenarios.
    /// </summary>
    public static Distribution<OrganizationUserStatusType> AllConfirmed { get; } = new(
        (OrganizationUserStatusType.Confirmed, 1.0)
    );

    /// <summary>
    /// New organization with many pending invites.
    /// </summary>
    public static Distribution<OrganizationUserStatusType> NewOrganization { get; } = new(
        (OrganizationUserStatusType.Confirmed, 0.30),
        (OrganizationUserStatusType.Invited, 0.50),
        (OrganizationUserStatusType.Accepted, 0.20)
    );
}
