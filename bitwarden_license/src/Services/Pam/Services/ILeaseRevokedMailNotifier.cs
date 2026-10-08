using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Services;

/// <summary>Emails a lease holder that an operator revoked their access. Never throws.</summary>
public interface ILeaseRevokedMailNotifier
{
    /// <summary>
    /// Mails only when <paramref name="endAction" /> is <see cref="AccessLeaseAction.Revoked" />, not when the holder
    /// ended their own access.
    /// </summary>
    Task NotifyLeaseEndedAsync(AccessLease lease, AccessLeaseAction endAction);
}
