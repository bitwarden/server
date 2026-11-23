namespace Bit.Core.Pam.Services;

/// <summary>
/// Records the vault actions that release a leasing-gated cipher without a lease: a collection change that leaves a
/// gated cipher with an ungated path, and a whole-vault export. <see cref="ICipherLeaseGate"/> decides what is gated.
/// </summary>
public interface ILeasingBypassAudit
{
    /// <summary>
    /// Runs <paramref name="change"/>, then records each of <paramref name="cipherIds"/> that was gated before it and
    /// is not after it. Every write that changes a cipher's collections goes through this.
    /// </summary>
    Task AuditCollectionChangeAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> cipherIds,
        Func<Task> change);

    /// <summary>
    /// Records a whole-vault export when it carries a gated cipher.
    /// </summary>
    Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> exportedCipherIds);
}
