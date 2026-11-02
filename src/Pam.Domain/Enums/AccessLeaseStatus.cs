namespace Bit.Pam.Enums;

/// <summary>
/// A lease's lifecycle state as of a read clock, never stored. Derived by
/// <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>; only <see cref="Active"/> leases authorize access.
/// </summary>
public enum AccessLeaseStatus : byte
{
    Active = 0,

    Expired = 1,

    /// <summary>An operator ended the lease early.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early.</summary>
    Cancelled = 3,
}
