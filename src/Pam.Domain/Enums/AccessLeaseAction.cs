namespace Bit.Pam.Enums;

/// <summary>
/// The action a party has taken on an <see cref="Entities.AccessLease"/>, if any. A lease is born <em>running</em>,
/// and ending it early is the only act that changes that — so the action set is all-terminal. Minting is recorded by
/// the row itself, and an extension changes the window (<c>NotAfter</c>, in place), not the lease's standing. Nothing
/// here ever comes from the clock — Active vs Expired is the read model's call
/// (<see cref="AccessStatusDerivation.ComputeLeaseStatus"/>).
/// </summary>
public enum AccessLeaseAction : byte
{
    /// <summary>No early end recorded; Active vs Expired is the clock's call at read time.</summary>
    None = 0,

    // Byte 1 (the old stored Expired) stays unused so Revoked/Cancelled keep their stored values and stay aligned with AccessRequestAction.Denied/Cancelled.

    /// <summary>An operator ended the lease early; RevokedBy/RevokedDate carry who and when.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early; RevokedBy/RevokedDate carry who and when.</summary>
    Cancelled = 3,
}
