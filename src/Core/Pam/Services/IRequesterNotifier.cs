namespace Bit.Pam.Services;

/// <summary>
/// Pushes the <c>RefreshAccessRequest</c> signal to a single requester, telling their clients to re-fetch their own
/// access requests and active leases. Fired whenever something the requester's view renders changes for a reason
/// other than their own local action — a pending request being decided, a held lease being revoked or extended, a
/// request being cancelled — so their "My requests" list, lease banner, and row badges stay live, and an open cipher
/// re-locks the moment its lease ends.
/// </summary>
public interface IRequesterNotifier
{
    Task NotifyRequesterAsync(Guid requesterId);
}
