namespace Bit.Core.Vault.Authorization;

/// <summary>
/// A capability that authorizes returning a cipher's <em>full</em> secret data under PAM credential
/// leasing. It is minted only by the leasing gate (<c>ICipherLeaseGate</c>) — application code cannot
/// fabricate one — and is required by the constructors of the <c>Full*</c> cipher response models. This
/// makes emitting full secret data a deliberate, type-checked act: the default (partial) response
/// shapes need no witness, so a path that forgets to obtain one fails closed.
/// </summary>
public sealed class FullCipherAccess
{
    private readonly bool _unrestricted;
    private readonly HashSet<Guid> _authorizedCipherIds;

    private FullCipherAccess(bool unrestricted, HashSet<Guid>? authorizedCipherIds)
    {
        _unrestricted = unrestricted;
        _authorizedCipherIds = authorizedCipherIds ?? new HashSet<Guid>();
    }

    /// <summary>
    /// Authorizes full data for any cipher. Minted by the gate for contexts that have already been
    /// authorized out-of-band (org admins, personal vaults, the flag-off no-op path).
    /// </summary>
    internal static FullCipherAccess Unrestricted() => new(unrestricted: true, authorizedCipherIds: null);

    /// <summary>Authorizes full data for exactly the given cipher.</summary>
    internal static FullCipherAccess ForCipher(Guid cipherId) => new(unrestricted: false, [cipherId]);

    /// <summary>Authorizes full data for exactly the given set of ciphers.</summary>
    internal static FullCipherAccess ForCiphers(IEnumerable<Guid> cipherIds) =>
        new(unrestricted: false, cipherIds.ToHashSet());

    /// <summary>Whether this witness authorizes full data for <paramref name="cipherId"/>.</summary>
    public bool Authorizes(Guid cipherId) => _unrestricted || _authorizedCipherIds.Contains(cipherId);

    /// <summary>
    /// Throws when this witness does not authorize <paramref name="cipherId"/>. Called by the
    /// <c>Full*</c> response model constructors so a full response cannot be built for a cipher the
    /// witness does not cover — keeping bulk lists fail-closed per element, not just at single reads.
    /// </summary>
    public void Require(Guid cipherId)
    {
        if (!Authorizes(cipherId))
        {
            throw new InvalidOperationException(
                "A full cipher response was constructed for a cipher the caller is not authorized to " +
                "read in full. This indicates a credential-leasing filtering bug.");
        }
    }
}
