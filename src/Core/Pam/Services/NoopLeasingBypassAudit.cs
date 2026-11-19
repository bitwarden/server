namespace Bit.Core.Pam.Services;

/// <summary>
/// Open-source fallback for <see cref="ILeasingBypassAudit"/>. Nothing is gated without the commercial Pam library,
/// so there is nothing to audit.
/// </summary>
public class NoopLeasingBypassAudit : ILeasingBypassAudit
{
    public Task<IReadOnlySet<Guid>> GetGatedCipherIdsAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
        => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public Task AuditUngatedAsync(Guid organizationId, Guid actingUserId, IReadOnlySet<Guid> gatedCipherIds)
        => Task.CompletedTask;

    public Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> exportedCipherIds)
        => Task.CompletedTask;
}
