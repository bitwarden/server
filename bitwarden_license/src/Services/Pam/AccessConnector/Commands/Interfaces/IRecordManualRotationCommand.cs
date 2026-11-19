namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRecordManualRotationCommand
{
    /// <summary>
    /// Records that an operator rotated a manual-target config's credential out of band (spec
    /// <c>RecordManualRotation</c>), clearing <c>awaiting_manual_rotation</c>.
    /// </summary>
    Task RecordAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
