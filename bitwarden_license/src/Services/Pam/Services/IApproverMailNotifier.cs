using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails the approvers of a collection when a request there is waiting on their decision: the out-of-band twin of
/// the <c>RefreshApproverInbox</c> push from <see cref="IApproverInboxNotifier" />, which only reaches a client that
/// is already open.
/// </summary>
/// <remarks>
/// Fired only from the human-approval path; an automatically approved request has no approver waiting.
///
/// Like <see cref="IAccessMailNotifier" />, it never throws: the call sits inside the command that creates the
/// access request, and a mail outage must not stop people requesting access to their own items.
/// </remarks>
public interface IApproverMailNotifier
{
    /// <summary>
    /// Notifies everyone who can Manage <paramref name="request" />'s collection that it needs a decision, except
    /// the requester: they may well manage it, but no one may decide their own request.
    /// </summary>
    Task NotifyPendingRequestAsync(AccessRequest request);
}
