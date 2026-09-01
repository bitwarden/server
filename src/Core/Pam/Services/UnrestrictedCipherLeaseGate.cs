using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// Open-source fallback for <see cref="ICipherLeaseGate"/>. PAM credential leasing is a commercial
/// feature, so in builds without the commercial implementation the gate never gates: every cipher is
/// fully readable and freely mutable, matching the behaviour when the PAM feature flag is off. The real
/// gating logic lives in the commercial Pam library.
/// </summary>
public class UnrestrictedCipherLeaseGate : ICipherLeaseGate
{
    public Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher)
        => Task.FromResult<FullCipherAccess?>(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher)
        => Task.FromResult<FullCipherAccess?>(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(Guid userId, Guid organizationId, Cipher cipher)
        => Task.FromResult<FullCipherAccess?>(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher)
        => Task.FromResult<FullCipherAccess?>(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> AuthorizeAdminReadManyAsync(
        Guid userId,
        Guid organizationId,
        IEnumerable<Cipher> ciphers)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public FullCipherAccess UnrestrictedForWholeVaultExport() => FullCipherAccess.Unrestricted();
}
