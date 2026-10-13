namespace Bit.Services.Pam.Services;

/// <summary>Pushes <c>RefreshAccessRequest</c> to a requester's devices.</summary>
public interface IRequesterNotifier
{
    Task NotifyRequesterAsync(Guid requesterId);
}
