namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IPauseRotationCommand
{
    /// <summary>Pauses a rotation config (spec <c>PauseRotation</c>). Guard: the config must currently be enabled.</summary>
    Task PauseAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
