using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IPamRotationConfigRepository : IRepository<PamRotationConfig, Guid>
{
    /// <summary>Returns the config for the cipher (invariant <c>OneConfigPerCipher</c>), or null.</summary>
    Task<PamRotationConfig?> GetByCipherIdAsync(Guid cipherId);

    /// <summary>
    /// Returns a single config's <see cref="PamRotationConfigDetails"/> projection, or null.
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
    /// Whether any config names the target system — the guard <c>DeleteTargetSystemCommand</c> checks before a
    /// target may be removed.
    /// </summary>
    Task<bool> AnyByTargetSystemAsync(Guid targetSystemId);

    /// <summary>
    /// Whether any config on the target system has <see cref="PamRotationConfig.TerminateSessions"/> set — the
    /// guard <c>UpdateTargetSystemPolicyCommand</c> checks before a target may withdraw
    /// <see cref="Entities.PamTargetSystem.SupportsSessionTermination"/>.
    /// </summary>
    Task<bool> AnyByTargetSystemWithTerminateSessionsAsync(Guid targetSystemId);

    /// <summary>
    /// Deletes the config's jobs and attempts, then the config itself, in one transaction. Re-checks under lock
    /// that the config still has no active job, returning false without deleting if one appeared after the
    /// caller's own check.
    /// </summary>
    Task<bool> DeleteWithJobsAsync(Guid configId);
}
