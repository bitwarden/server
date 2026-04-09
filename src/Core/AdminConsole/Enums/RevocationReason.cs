namespace Bit.Core.Enums;

/// <summary>
/// The reason a user was revoked from an organization.
/// </summary>
public enum RevocationReason : byte
{
    /// <summary>
    /// Revoked for an unknown reason, or migrated from before revocation reasons were tracked.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Manually revoked by an administrator.
    /// </summary>
    Manual = 1,
    /// <summary>
    /// Revoked because the user violated the two-factor authentication policy.
    /// </summary>
    TwoFactorPolicyNonCompliance = 2,
    /// <summary>
    /// Revoked because the user violated the organization data ownership policy.
    /// </summary>
    OrganizationDataOwnershipPolicyNonCompliance = 3,
    /// <summary>
    /// Revoked because the user violated the single organization policy.
    /// </summary>
    SingleOrgPolicyNonCompliance = 4,
}
