namespace Bit.Pam.Enums;

/// <summary>
/// A request's position in its lifecycle <em>as of a read clock</em> — pure read-model vocabulary, never stored.
/// Produced only by <see cref="AccessStatusDerivation.ComputeStatus"/> from the stored
/// <see cref="AccessRequestAction"/>; entities never carry it. Activation is not a state of its own: a request
/// promoted to a lease stays <see cref="Approved"/>, and the produced lease is what records that it happened.
/// </summary>
public enum AccessRequestStatus : byte
{
    /// <summary>Open (no action recorded) and still answerable — its window has not lapsed.</summary>
    Pending = 0,

    /// <summary>
    /// Approved automatically or by an approver, and still standing: activatable while its window is open, activated
    /// (the story continues on the lease), or an applied extension (which did its work at creation).
    /// </summary>
    Approved = 1,

    /// <summary>An approver refused the request, or retracted a not-yet-activated approval; no lease is produced.</summary>
    Denied = 2,

    /// <summary>Withdrawn by the requester.</summary>
    Cancelled = 3,

    /// <summary>
    /// The window lapsed with nothing to show for it: either nobody answered an open request, or an approval was
    /// never activated. The two origins share this one value; consumers distinguish them via the decision log
    /// (empty = unanswered, contains an approval = unactivated).
    /// </summary>
    Expired = 4,
}
