namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessLease"/>. Only <see cref="Active"/> leases authorize access.
/// </summary>
public enum AccessLeaseStatus : byte
{
    Active = 0,
    Expired = 1,
    Revoked = 2,

    /// <summary>The holder ended their own lease early, as opposed to <see cref="Revoked"/> (an operator ended it).</summary>
    Cancelled = 3,
}
