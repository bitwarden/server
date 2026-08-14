namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessRequest"/>. A request starts <see cref="Pending"/> and moves to exactly
/// one terminal state. Auto-approved requests are created already <see cref="Approved"/>. Activation is not a state of
/// its own: a request promoted to a lease stays <see cref="Approved"/>, and the produced lease is what records that it
/// happened.
/// </summary>
public enum AccessRequestStatus : byte
{
    /// <summary>Opened and awaiting a human approver's decision.</summary>
    Pending = 0,

    /// <summary>Approved automatically or by an approver; the requester can activate it into a lease within its window.</summary>
    Approved = 1,

    /// <summary>An approver refused the request; no lease is produced.</summary>
    Denied = 2,

    /// <summary>Withdrawn by the requester before it was decided.</summary>
    Cancelled = 3,

    /// <summary>The approval window lapsed with no decision recorded: nobody answered.</summary>
    Expired = 4,
}
