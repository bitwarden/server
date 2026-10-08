using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IPamRotationConfigRepository : IRepository<PamRotationConfig, Guid>
{
    Task<PamRotationConfig?> GetByCipherIdAsync(Guid cipherId);

    Task<PamRotationConfigDetails?> GetDetailsByIdAsync(Guid id);

    Task<ICollection<PamRotationConfigDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Configs due for a scheduled offer: enabled, on an active automatic target, due by <paramref name="now"/>, and
    /// with no active job.
    /// </summary>
    Task<ICollection<PamRotationConfig>> GetManyDueAsync(DateTime now);

    /// <summary>Whether any config names the target system, which blocks deleting it.</summary>
    Task<bool> AnyByTargetSystemAsync(Guid targetSystemId);

    /// <summary>
    /// Whether any config on the target system sets <see cref="PamRotationConfig.TerminateSessions"/>, which blocks
    /// the target from withdrawing session-termination support.
    /// </summary>
    Task<bool> AnyByTargetSystemWithTerminateSessionsAsync(Guid targetSystemId);

    /// <summary>
    /// Deletes the config's attempts, jobs, and then the config in one transaction. Returns false without deleting
    /// when the re-check under lock finds an active job.
    /// </summary>
    Task<bool> DeleteWithJobsAsync(Guid configId);
}
