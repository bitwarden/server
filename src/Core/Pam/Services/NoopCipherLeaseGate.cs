using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Pam.Services;

/// <summary>
/// Open-source fallback for <see cref="ICipherLeaseGate"/>. PAM credential leasing is a commercial feature, so in
/// builds without the commercial implementation the gate never gates: every cipher is fully accessible, matching
/// the behaviour when the PAM feature flag is off. The real gating logic lives in the commercial Pam library.
/// </summary>
public class NoopCipherLeaseGate : ICipherLeaseGate
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

    public ISet<Guid> GetGatedCipherIds(
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
        => new HashSet<Guid>();

    public Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
        => Task.FromResult(FullCipherAccess.Unrestricted());

    public FullCipherAccess Unrestricted() => FullCipherAccess.Unrestricted();
}
