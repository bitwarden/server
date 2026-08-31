using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a requester the verdict on their pending access request — the out-of-band twin of the
/// <c>RefreshAccessRequest</c> push that <see cref="IRequesterNotifier" /> sends. The push only reaches a client that
/// is already open, so without this the answer someone is blocked on is visible nowhere but the web vault they would
/// have to keep watching.
/// </summary>
/// <remarks>
/// The requester is the only recipient. The approver is the actor — they just pressed the button — and mailing
/// someone about their own action is the noise that gets a notification channel muted, so the approver's identity is
/// not a parameter here and cannot reach a recipient list.
///
/// Like <see cref="IAccessMailNotifier" />, it never throws: the call sits inside the command that records the
/// decision, and a mail outage must not fail an approval that has already been written.
/// </remarks>
public interface IRequesterMailNotifier
{
    /// <summary>
    /// Tells <paramref name="request" />'s requester that it was resolved.
    /// </summary>
    /// <param name="request">The request just resolved. Its <c>Action</c> may not be stamped yet at the call site.</param>
    /// <param name="approved">
    /// The verdict, passed rather than read from <paramref name="request" /> for that reason. It selects one of the
    /// mail's two bodies; an approval is a startable approval, not access already granted.
    /// </param>
    Task NotifyDecisionAsync(AccessRequest request, bool approved);
}
