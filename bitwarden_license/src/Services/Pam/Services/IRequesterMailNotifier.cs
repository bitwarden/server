using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a requester the verdict on their pending request, since the push only reaches open clients. Never throws, so
/// a mail outage cannot fail a written decision.
/// </summary>
public interface IRequesterMailNotifier
{
    /// <param name="approved">
    /// Passed because <paramref name="request" />'s <c>Action</c> may not be stamped yet at the call site.
    /// </param>
    Task NotifyDecisionAsync(AccessRequest request, bool approved);
}
