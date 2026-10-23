namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IPauseRotationCommand
{
    /// <summary>Pauses an enabled rotation config (spec <c>PauseRotation</c>).</summary>
    Task PauseAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
