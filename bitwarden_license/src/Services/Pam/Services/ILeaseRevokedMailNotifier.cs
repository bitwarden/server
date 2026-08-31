using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a lease holder that an operator ended their active access — the out-of-band twin of the
/// <c>RefreshAccessRequest</c> push that <see cref="IRequesterNotifier" /> sends on the same path. The push has
/// already re-locked the item on any client that happened to be open; this tells the person mid-task why.
/// </summary>
/// <remarks>
/// A courtesy, not a security control. The lease is dead server-side before anything here runs, so nothing this
/// type does or fails to do changes who holds access.
///
/// Like <see cref="IAccessMailNotifier" />, it never throws: the call sits at the end of the command that ended
/// the lease, and a mail outage must not fail a revocation that has already been written.
/// </remarks>
public interface ILeaseRevokedMailNotifier
{
    /// <summary>
    /// Tells <paramref name="lease" />'s holder that their access ended, but only when
    /// <paramref name="endAction" /> is <see cref="AccessLeaseAction.Revoked" />.
    /// </summary>
    /// <remarks>
    /// Handed every early end rather than only the revocations so the rule that a holder is never mailed about
    /// their own action lives in one place. Mailing someone "your access was revoked" seconds after they ended it
    /// themselves is the kind of notification that teaches people to ignore the channel.
    /// </remarks>
    /// <param name="lease">The lease just ended. Its <c>Action</c> may not be stamped yet at the call site.</param>
    /// <param name="endAction">
    /// How it ended, passed rather than read from <paramref name="lease" /> for that reason:
    /// <see cref="AccessLeaseAction.Revoked" /> for an operator, <see cref="AccessLeaseAction.Cancelled" /> for the
    /// holder ending their own access.
    /// </param>
    Task NotifyLeaseEndedAsync(AccessLease lease, AccessLeaseAction endAction);
}
