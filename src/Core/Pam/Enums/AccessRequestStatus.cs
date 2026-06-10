namespace Bit.Core.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.AccessRequest"/>. A request starts <see cref="Pending"/> and moves to exactly
/// one terminal state. Auto-approved requests are created already <see cref="Approved"/>.
/// </summary>
public enum AccessRequestStatus : byte
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Cancelled = 3,
    ExpiredUnanswered = 4,
}
