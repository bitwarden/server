namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessLease"/>. Only <see cref="Active"/> leases authorize access.
/// </summary>
public enum AccessLeaseStatus : byte
{
    /// <summary>Live; the access window is open and the lease has not been ended early, so it authorizes access.</summary>
    Active = 0,

    /// <summary>The lease's window closed on its own.</summary>
    Expired = 1,

    /// <summary>An operator ended the lease early, before its window closed.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early, as opposed to <see cref="Revoked"/> (an operator ended it).</summary>
    Cancelled = 3,
}
