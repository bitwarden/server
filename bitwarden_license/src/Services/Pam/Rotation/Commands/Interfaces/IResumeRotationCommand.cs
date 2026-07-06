namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IResumeRotationCommand
{
    /// <summary>
    /// Resumes a paused rotation config (spec <c>ResumeRotation</c>). Guard: the config must currently be disabled.
    /// A manual-target config with a due obligation (<c>NextRotationAt &lt;= now</c>, read while paused) has that
    /// obligation pulled due (<c>NextRotationAt = now</c>); otherwise <c>NextRotationAt</c> is recomputed from the
    /// schedule.
    /// </summary>
    Task ResumeAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
