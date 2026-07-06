namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IRecordManualRotationCommand
{
    /// <summary>
    /// Records that an operator rotated a manual-target config's credential out of band (spec
    /// <c>RecordManualRotation</c>) — clears <c>awaiting_manual_rotation</c>. Guard: the config's target system must
    /// be <see cref="Bit.Pam.Enums.PamTargetSystemMethod.Manual"/>.
    /// </summary>
    Task RecordAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
