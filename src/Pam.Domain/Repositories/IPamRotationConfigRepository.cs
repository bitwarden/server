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
    /// Deletes the config together with its jobs and attempts. Returns false without deleting when the re-check under
    /// lock finds an active job. The only supported delete; <c>DeleteAsync</c> throws.
    /// </summary>
    Task<bool> DeleteWithJobsAsync(Guid configId);
}
