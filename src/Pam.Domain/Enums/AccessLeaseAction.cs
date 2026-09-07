namespace Bit.Pam.Enums;

/// <summary>
/// The action a party has taken on an <see cref="Entities.AccessLease"/>, if any. A lease is born <em>running</em>,
/// so the action set is all-terminal; Active vs Expired is derived by the read model instead
/// (<see cref="AccessStatusDerivation.ComputeLeaseStatus"/>), never stored here.
/// </summary>
public enum AccessLeaseAction : byte
{
    /// <summary>No early end recorded; Active vs Expired is the clock's call at read time.</summary>
    None = 0,

    // Byte 1 (the old stored Expired) stays unused so Revoked/Cancelled keep their stored values and stay aligned with AccessRequestAction.Denied/Cancelled.

    /// <summary>An operator ended the lease early; RevokedBy/RevokedDate record who and the timestamp.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early; RevokedBy/RevokedDate record who and the timestamp.</summary>
    Cancelled = 3,
}
