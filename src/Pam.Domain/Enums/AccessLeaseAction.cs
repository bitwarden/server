namespace Bit.Pam.Enums;

/// <summary>
/// The early end recorded on an <see cref="Entities.AccessLease"/>, if any. A lease starts running, so every action
/// ends it; Active vs Expired is derived by <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>.
/// </summary>
public enum AccessLeaseAction : byte
{
    None = 0,

    // 1 is unused: Revoked/Cancelled keep their stored values, which match AccessRequestAction.Denied/Cancelled.

    /// <summary>An operator ended the lease early, recorded in RevokedBy and RevokedDate.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early, also recorded in RevokedBy and RevokedDate.</summary>
    Cancelled = 3,
}
