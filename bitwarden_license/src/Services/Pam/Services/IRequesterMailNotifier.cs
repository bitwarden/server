using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a requester the verdict on their pending access request: the out-of-band twin of the
/// <c>RefreshAccessRequest</c> push from <see cref="IRequesterNotifier" />, which only reaches a client that is
/// already open.
/// </summary>
/// <remarks>
/// The requester is the only recipient. The approver just pressed the button, so their identity is not a parameter
/// here and cannot reach a recipient list.
///
/// Like <see cref="IAccessMailNotifier" />, it never throws: the call sits inside the command that records the
/// decision, and a mail outage must not fail an approval that has already been written.
/// </remarks>
public interface IRequesterMailNotifier
{
    /// <param name="request">The request just resolved. Its <c>Action</c> may not be stamped yet at the call site.</param>
    /// <param name="approved">The verdict, passed rather than read from <paramref name="request" /> for that reason.</param>
    Task NotifyDecisionAsync(AccessRequest request, bool approved);
}
