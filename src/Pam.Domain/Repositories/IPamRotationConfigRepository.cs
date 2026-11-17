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

    /// <summary>
    /// Configs with an access end still owed its trigger (spec <c>pending_access_end</c>), recorded at or before
    /// <paramref name="createdBefore"/>.
    /// </summary>
    Task<ICollection<PamRotationConfig>> GetManyPendingAccessEndAsync(DateTime createdBefore);

    /// <summary>Clears the config's pending access ends once their trigger has run.</summary>
    Task DeletePendingAccessEndAsync(Guid configId);

    /// <summary>
    /// Clears a manual config's pending access ends and pulls its rotation due at <paramref name="now"/>, in one write.
    /// Returns false, changing nothing, when none was pending.
    /// </summary>
    Task<bool> RaiseManualObligationOnAccessEndAsync(Guid configId, DateTime now);

    /// <summary>Whether any config names the target system, which blocks deleting it.</summary>
    Task<bool> AnyByTargetSystemAsync(Guid targetSystemId);

    /// <summary>
    /// Deletes the config together with its jobs and attempts. Returns false without deleting when the re-check under
    /// lock finds an active job. The only supported delete; <c>DeleteAsync</c> throws.
    /// </summary>
    Task<bool> DeleteWithJobsAsync(Guid configId);
}
