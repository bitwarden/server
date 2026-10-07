namespace Bit.Services.Pam.Services;

/// <summary>
/// Pushes <c>RefreshAccessRequest</c> to a requester so their clients re-fetch their access requests and active
/// leases. Also fired for the requester's own actions, which their other devices need to see.
/// </summary>
public interface IRequesterNotifier
{
    Task NotifyRequesterAsync(Guid requesterId);
}
