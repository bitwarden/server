using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails the approvers of a collection when a request there is waiting on their decision — the out-of-band twin of
/// the <c>RefreshApproverInbox</c> push that <see cref="IApproverInboxNotifier" /> sends. The push only reaches a
/// client that is already open, so without this an approver can be the sole reason a requester is blocked and never
/// learn they were asked.
/// </summary>
/// <remarks>
/// Fired only from the human-approval path. An automatically approved request has no approver waiting on anything,
/// so it sends nothing.
///
/// Like <see cref="IAccessMailNotifier" />, it never throws: the call sits inside the command that creates the
/// access request, and a mail outage must not stop people requesting access to their own items.
/// </remarks>
public interface IApproverMailNotifier
{
    /// <summary>
    /// Notifies everyone who can Manage <paramref name="request" />'s collection that it needs a decision, except
    /// the requester themselves — they may well manage it, but no one may decide their own request.
    /// </summary>
    /// <remarks>
    /// One email per request, always. Every request therefore fans out to every manager of the collection, which is
    /// deliberate: an approval is time-critical, and a request that is quietly folded into a summary is one nobody
    /// is told about individually.
    /// </remarks>
    Task NotifyPendingRequestAsync(AccessRequest request);
}
