namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessLease"/>. Only <see cref="Active"/> leases authorize access.
/// </summary>
public enum AccessLeaseStatus : byte
{
    /// <summary>Live; within its window it authorizes access.</summary>
    Active = 0,

    /// <summary>The lease's window ended on its own.</summary>
    Expired = 1,

    /// <summary>An operator ended the lease early.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early, as opposed to <see cref="Revoked"/> (an operator ended it).</summary>
    Cancelled = 3,
}
