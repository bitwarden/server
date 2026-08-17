namespace Bit.Services.Pam.Services;

/// <summary>
/// Placeholder implementation of <see cref="IRequesterNotifier"/> that pushes nothing.
///
/// The signal it would send (<c>RefreshAccessRequest</c>) is not a push type this slice defines, and the clients do
/// not subscribe to it yet, so a requester's view reflects a decision on its next fetch rather than live. The commands
/// still notify through this seam, so landing the push type is a matter of swapping the registration.
/// </summary>
public class NoopRequesterNotifier : IRequesterNotifier
{
    public Task NotifyRequesterAsync(Guid requesterId) => Task.CompletedTask;
}
