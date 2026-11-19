namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IHandleAccessGrantEndedCommand
{
    /// <summary>
    /// Reacts to a lease on <paramref name="cipherId"/> ending. Gated on
    /// <see cref="Bit.Core.FeatureFlagKeys.PamAccessConnector"/>; for an enabled config that opts in, offers an
    /// access-end job on an automatic target or pulls a manual target's rotation due.
    /// </summary>
    Task HandleAsync(Guid cipherId);
}
