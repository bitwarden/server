using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a collection's approvers when a request there awaits their decision, since the push only reaches open
/// clients. Never throws, so a mail outage cannot block requesting access.
/// </summary>
public interface IApproverMailNotifier
{
    /// <summary>
    /// Notifies everyone who can Manage <paramref name="request" />'s collection, except the requester, who cannot
    /// decide their own request.
    /// </summary>
    Task NotifyPendingRequestAsync(AccessRequest request);
}
