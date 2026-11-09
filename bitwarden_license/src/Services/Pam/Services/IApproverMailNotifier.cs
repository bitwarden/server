using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

/// <summary>Emails a collection's approvers when a request awaits their decision. Never throws.</summary>
public interface IApproverMailNotifier
{
    /// <summary>Mails everyone who can Manage the request's collection, except the requester.</summary>
    Task NotifyPendingRequestAsync(AccessRequest request);
}
