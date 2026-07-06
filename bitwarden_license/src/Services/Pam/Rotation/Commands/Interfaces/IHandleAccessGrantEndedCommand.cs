namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IHandleAccessGrantEndedCommand
{
    /// <summary>
    /// Reacts to a lease on <paramref name="cipherId"/> ending — revoke, self-end, or natural expiry (spec
    /// <c>RotateOnAccessEnd</c> / <c>RaiseManualObligationOnAccessEnd</c>). No-op when the
    /// <see cref="Bit.Core.FeatureFlagKeys.PamRotation"/> flag is off, when the cipher has no config, when the
    /// config does not opt in (<c>RotateOnAccessEnd</c>), or when the config is paused/disabled (the deferred
    /// access-end latch is out of scope this pass). On an automatic target, offers a job
    /// (<see cref="Bit.Pam.Enums.PamRotationSource.AccessEnd"/>); on a manual target, pulls the obligation due
    /// (<c>NextRotationAt = now</c>).
    /// </summary>
    Task HandleAsync(Guid cipherId);
}
