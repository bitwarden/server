namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IResumeRotationCommand
{
    /// <summary>
    /// Resumes a paused rotation config, recomputing <c>NextRotationAt</c> from the schedule unless a manual rotation
    /// is already due.
    /// </summary>
    Task ResumeAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
