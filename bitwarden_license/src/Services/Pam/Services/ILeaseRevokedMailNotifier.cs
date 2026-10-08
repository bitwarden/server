using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Emails a lease holder that an operator ended their access. The email is a courtesy, since the lease has already
/// ended server-side. Never throws, so a mail outage cannot fail a written revocation.
/// </summary>
public interface ILeaseRevokedMailNotifier
{
    /// <summary>
    /// Mails <paramref name="lease" />'s holder only when <paramref name="endAction" /> is
    /// <see cref="AccessLeaseAction.Revoked" />; a holder ending their own access is not mailed.
    /// </summary>
    /// <param name="endAction">
    /// Passed because <paramref name="lease" />'s <c>Action</c> may not be stamped yet at the call site.
    /// </param>
    Task NotifyLeaseEndedAsync(AccessLease lease, AccessLeaseAction endAction);
}
