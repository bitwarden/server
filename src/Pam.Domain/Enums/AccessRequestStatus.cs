namespace Bit.Pam.Enums;

/// <summary>
/// A request's lifecycle state as of a read clock, never stored; derived by
/// <see cref="AccessStatusDerivation.ComputeStatus"/>. An activated request stays <see cref="Approved"/>, and the
/// produced lease records the activation.
/// </summary>
public enum AccessRequestStatus : byte
{
    Pending = 0,

    /// <summary>Activatable while its window is open, already activated, or an applied extension.</summary>
    Approved = 1,

    /// <summary>Refused, or retracted by an approver before activation; no lease is produced.</summary>
    Denied = 2,

    /// <summary>Withdrawn by the requester.</summary>
    Cancelled = 3,

    /// <summary>
    /// The window lapsed while the request was unanswered or its approval unactivated. An empty decision log means
    /// unanswered; one holding an approval means unactivated.
    /// </summary>
    Expired = 4,
}
