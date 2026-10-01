namespace Bit.Services.Pam.Services;

/// <summary>
/// Pushes the <c>RefreshAccessRequest</c> signal to a single requester, telling their clients to re-fetch their
/// own access requests and active leases. Fired for a change the requester didn't make themselves, such as a
/// request being decided or a lease being revoked or extended.
/// </summary>
public interface IRequesterNotifier
{
    Task NotifyRequesterAsync(Guid requesterId);
}
