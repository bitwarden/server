namespace Bit.Core.Pam.Services;

/// <summary>
/// Audits the vault actions that release a leasing-gated cipher without a lease: a whole-vault export, and a
/// collection change that leaves a gated cipher with an ungated path.
/// </summary>
/// <remarks>
/// Gated here is organization-wide and ignores leases, as in the "/admin" decisions of <see cref="ICipherLeaseGate"/>.
/// </remarks>
public interface ILeasingBypassAudit
{
    /// <summary>
    /// The subset of <paramref name="cipherIds"/> that is gated. Taken before a collection change, for
    /// <see cref="AuditUngatedAsync"/> to compare against after it.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetGatedCipherIdsAsync(Guid organizationId, IEnumerable<Guid> cipherIds);

    /// <summary>
    /// Records each of <paramref name="gatedCipherIds"/> that is no longer gated.
    /// </summary>
    Task AuditUngatedAsync(Guid organizationId, Guid actingUserId, IReadOnlySet<Guid> gatedCipherIds);

    /// <summary>
    /// Records a whole-vault export when it carries a gated cipher.
    /// </summary>
    Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> exportedCipherIds);
}
