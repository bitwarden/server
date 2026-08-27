namespace Bit.Pam.Enums;

/// <summary>
/// The action a party has taken on an <see cref="Entities.AccessRequest"/>, if any. Facts about what was recorded,
/// not about current standing: <see cref="Approved"/> stays <see cref="Approved"/> forever, even after the window
/// lapses or the lease it minted ends. Nothing here ever comes from the clock — what a recorded action <em>means
/// right now</em> is the read model's job (<see cref="AccessRequestStatus"/>, produced by
/// <see cref="AccessStatusDerivation.ComputeStatus"/>).
/// </summary>
public enum AccessRequestAction : byte
{
    /// <summary>Nothing recorded; the request is open. Pending vs Expired is the clock's call at read time.</summary>
    None = 0,

    /// <summary>An approver (or the rule engine, on the automatic and extension paths) approved the request.</summary>
    Approved = 1,

    /// <summary>An approver refused the request, or retracted a not-yet-activated approval.</summary>
    Denied = 2,

    /// <summary>The requester withdrew their own request. Only the requester writes this.</summary>
    Cancelled = 3,
}
