using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IPamRotationConfigRepository : IRepository<PamRotationConfig, Guid>
{
    /// <summary>Returns the config for the cipher (invariant <c>OneConfigPerCipher</c>), or null if none exists.</summary>
    Task<PamRotationConfig?> GetByCipherIdAsync(Guid cipherId);

    /// <summary>
    /// Returns a single config's <see cref="PamRotationConfigDetails"/> projection (target display fields plus
    /// whether it has an active job), or null if no config has the id.
    /// </summary>
    Task<PamRotationConfigDetails?> GetDetailsByIdAsync(Guid id);

    Task<ICollection<PamRotationConfigDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the configs due for a scheduled offer (spec <c>RotationDue</c>): enabled, on an
    /// <see cref="Enums.PamTargetSystemMethod.Automatic"/> target that is <see cref="Enums.PamTargetSystemStatus.Active"/>,
    /// with <see cref="PamRotationConfig.NextRotationAt"/> at or before <paramref name="now"/>, and with no active
    /// job. Read by the sweep's due phase, one <c>OfferRotationCommand</c> call per row.
    /// </summary>
    Task<ICollection<PamRotationConfig>> GetManyDueAsync(DateTime now);

    /// <summary>
    /// Whether any config on the target system has <see cref="PamRotationConfig.TerminateSessions"/> set — the
    /// guard <c>UpdateTargetSystemPolicyCommand</c> checks before a target may withdraw
    /// <see cref="Entities.PamTargetSystem.SupportsSessionTermination"/>.
    /// </summary>
    Task<bool> AnyByTargetSystemWithTerminateSessionsAsync(Guid targetSystemId);

    /// <summary>
    /// Deletes the config's jobs and attempts, then the config itself, in one transaction — the durable history
    /// stays in the audit trail, not here. Called by <c>DeleteRotationConfigCommand</c> after it has confirmed the
    /// config has no active job.
    /// </summary>
    Task DeleteWithJobsAsync(Guid configId);
}
