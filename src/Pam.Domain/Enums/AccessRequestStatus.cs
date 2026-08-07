namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessRequest"/>. A request starts <see cref="Pending"/> and moves to exactly
/// one terminal state. Auto-approved requests are created already <see cref="Approved"/>.
/// </summary>
public enum AccessRequestStatus : byte
{
    /// <summary>Awaiting a human approver's decision.</summary>
    Pending = 0,

    /// <summary>Approved automatically or by an approver; the requester can activate it into a lease within its window.</summary>
    Approved = 1,

    /// <summary>An approver refused the request.</summary>
    Denied = 2,

    /// <summary>Withdrawn by the requester before it was decided.</summary>
    Cancelled = 3,

    /// <summary>The approval window lapsed with no decision recorded.</summary>
    ExpiredUnanswered = 4,
}
