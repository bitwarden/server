using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// The read decision point for PAM credential leasing in Vault code. A cipher reachable only through
/// leasing-enabled collections is "leasing-gated": its secrets are withheld (partial data) unless the
/// caller holds a valid active lease. Every method is "unrestricted" when the <c>Pam</c> feature flag is
/// off, so flag-off behaviour is unchanged.
/// </summary>
public interface ICipherLeaseGate
{
    /// <summary>
    /// Per-cipher read decision. Returns a <see cref="FullCipherAccess"/> witness authorizing full data
    /// when the caller may see it (not gated, or gated with a valid active lease), or <c>null</c> when
    /// the caller is blocked and must receive the partial shape.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk read decision. Returns a single witness authorizing full data for the non-gated subset of
    /// <paramref name="ciphers"/>, computed in-memory from the supplied collections and mappings (no
    /// per-cipher queries). Bulk reads strip <em>every</em> gated cipher regardless of lease state —
    /// secrets are only ever released through <see cref="AuthorizeReadAsync"/>.
    /// </summary>
    Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher);

    /// <summary>
    /// Self-loading variant of the bulk decision, for callers that have not already loaded the caller's
    /// collections and mappings. Loads them once — but only when the flag is on, so the flag-off path
    /// stays query-free.
    /// </summary>
    Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Mints an unrestricted witness for a context that has already been authorized out-of-band — org
    /// admins acting through org-wide permissions, personal vaults, and export flows the controller has
    /// already gated. Use deliberately; it authorizes full data for any cipher.
    /// </summary>
    FullCipherAccess Unrestricted();
}
