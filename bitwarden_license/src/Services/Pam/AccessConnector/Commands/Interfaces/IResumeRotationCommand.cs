namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IResumeRotationCommand
{
    /// <summary>
    /// Resumes a paused rotation config. Guard: the config must be disabled. A manual-target config with a
    /// due obligation has it pulled due (<c>NextRotationAt = now</c>); otherwise <c>NextRotationAt</c> is
    /// recomputed from the schedule.
    /// </summary>
    Task ResumeAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
