namespace Bit.Core.Pam.Services;

/// <summary>
/// Open-source fallback for <see cref="ILeasingBypassAudit"/>. Nothing is gated without the commercial Pam library,
/// so it runs the change and records nothing.
/// </summary>
public class NoopLeasingBypassAudit : ILeasingBypassAudit
{
    public Task AuditCollectionChangeAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> cipherIds,
        Func<Task> change) => change();

    public Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> exportedCipherIds)
        => Task.CompletedTask;
}
