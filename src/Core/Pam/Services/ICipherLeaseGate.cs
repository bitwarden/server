using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// The decision point for PAM credential leasing in Vault code. A cipher reachable only through
/// leasing-enabled collections is "leasing-gated": its secrets are withheld (partial data) unless the
/// caller holds a valid active lease, and mutating it is refused without one. Every method is
/// "unrestricted" when the <c>Pam</c> feature flag is off, so flag-off behaviour is unchanged.
/// </summary>
/// <remarks>
/// Every method is called with whatever mix of ciphers the caller holds — organization-owned and
/// user-owned alike — so an implementation must decide for both. Only a cipher reached through a
/// leasing-enabled collection can be gated, which means a user-owned cipher never is; an implementation
/// authorizes it rather than treating an absent organization as a reason to withhold.
///
/// Implementations decide; they never shape a response. The caller turns a decision into a response
/// shape (see <see cref="FullCipherAccess"/>), which is what keeps the authorization decision in the
/// controller and out of the response models.
/// </remarks>
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
    /// <param name="collections">
    /// The caller's collections. <c>null</c> means "not loaded, because the caller has no organizations"
    /// and is equivalent to empty: with no collection to reach a cipher through, nothing is gated.
    /// </param>
    /// <param name="collectionCiphersByCipher">
    /// The caller's cipher-to-collection mappings, keyed by cipher id. <c>null</c> carries the same
    /// meaning as for <paramref name="collections"/>; a cipher simply absent from a supplied dictionary
    /// is reachable through no collection and so is not gated.
    /// </param>
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
    /// Per-cipher write decision. Throws <see cref="NotFoundException"/> when mutating
    /// <paramref name="cipher"/> is refused — it is gated and the caller holds no valid active lease —
    /// and otherwise returns a witness authorizing that cipher, so a caller who echoes the mutated cipher
    /// back can build the full shape without asking again.
    /// </summary>
    /// <remarks>
    /// <see cref="NotFoundException"/>, not a forbidden, and deliberately so: a member who cannot reach a
    /// credential should not learn from a write attempt that it exists. It also matches how the vault
    /// already answers a mutation aimed at a cipher the caller cannot edit.
    /// </remarks>
    Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk write decision. Throws <see cref="NotFoundException"/> if <em>any</em> of
    /// <paramref name="ciphers"/> is gated with no valid active lease, refusing the whole batch rather
    /// than half-applying it; otherwise returns a witness authorizing all of them.
    /// </summary>
    /// <remarks>
    /// A held lease widens this decision, which is the opposite of
    /// <see cref="AuthorizeReadManyAsync(Guid, IEnumerable{Cipher})"/> — and for a reason. A bulk read
    /// stays strict because a sync copies secrets into every client's local store for as long as that
    /// store lives; a bulk write copies no secret anywhere, so refusing a lease-holder's own edit would
    /// withhold nothing and only break the feature for the person the lease was issued to.
    /// </remarks>
    Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Mints an unrestricted witness for a context that has already been authorized out-of-band — org
    /// admins acting through org-wide permissions, personal vaults, and export flows the controller has
    /// already gated. Use deliberately; it authorizes full data for any cipher.
    /// </summary>
    FullCipherAccess Unrestricted();
}
