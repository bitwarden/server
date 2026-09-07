namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IHandleAccessGrantEndedCommand
{
    /// <summary>
    /// Reacts to a lease on <paramref name="cipherId"/> ending — revoke, self-end, or natural expiry. No-op if
    /// the <see cref="Bit.Core.FeatureFlagKeys.PamAccessConnector"/> flag is off, the cipher has no config, or
    /// the config doesn't opt in or is paused/disabled. On an automatic target, offers a job
    /// (<see cref="Bit.Pam.Enums.PamRotationSource.AccessEnd"/>); on a manual target, pulls the obligation due.
    /// </summary>
    Task HandleAsync(Guid cipherId);
}
