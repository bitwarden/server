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
/// <remarks>
/// Callers reach a cipher in one of two stances, and the gate has a method family for each. A
/// <em>member</em> reaches it through their own collection assignments, which decide what they see. An
/// <em>administrator</em> reaches it through organization-wide permission: the endpoint returns the
/// organization's ciphers whatever the caller is assigned to, so their assignments decide nothing. They
/// may hold one for the collection in question or none at all. The stance decides where leasing status
/// is resolved from, not whether leasing applies: an owner or admin is subject to leasing like anyone
/// else.
///
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
    /// Per-cipher read for a caller reaching the cipher through their own collection assignments.
    /// Returns a <see cref="FullCipherAccess"/> witness authorizing full data when the caller may see it
    /// (not gated, or gated with a valid active lease), or <c>null</c> when the caller is blocked and
    /// must receive the partial shape.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk counterpart of <see cref="AuthorizeReadAsync"/>. Returns a single witness authorizing full
    /// data for the non-gated subset of <paramref name="ciphers"/>, computed in-memory from the supplied
    /// collections and mappings (no per-cipher queries). Bulk reads strip <em>every</em> gated cipher
    /// regardless of lease state — secrets are only ever released one cipher at a time.
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
    /// Self-loading variant of the bulk member decision, for callers that have not already loaded the
    /// caller's collections and mappings. Loads them once — but only when the flag is on, so the flag-off
    /// path stays query-free.
    /// </summary>
    Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Per-cipher read for a caller reaching the cipher through organization-wide permission rather than
    /// a collection assignment — the "/admin" endpoints.
    /// </summary>
    /// <remarks>
    /// Leasing status is resolved from the organization's collections, not the caller's, so a cipher the
    /// caller is not assigned to reach is still correctly identified as gated. Resolving it from the
    /// caller's collections instead would fail open for exactly those ciphers: the member decision reads
    /// an absent mapping as "reachable through no collection, therefore not gated".
    ///
    /// A lease can only exist for a collection the caller has access to, so a gated cipher the caller is
    /// not assigned to always yields the partial shape.
    /// </remarks>
    Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher);

    /// <summary>
    /// Bulk counterpart of <see cref="AuthorizeAdminReadAsync"/>, stripping every gated cipher regardless
    /// of lease state just as the member bulk decision does. Loads the organization's leasing-enabled
    /// collections once, and only when the flag is on.
    /// </summary>
    Task<FullCipherAccess> AuthorizeAdminReadManyAsync(
        Guid userId,
        Guid organizationId,
        IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Mints an unrestricted witness for whole-vault organization export — the only context in which
    /// leasing is waived, and so the only sanctioned way to obtain full data without a decision. A
    /// whole-vault exporter already holds an organization-wide read grant scoped to export, and a
    /// partially stripped export is not a usable backup. The caller establishes that the requester may
    /// export the whole vault; this only mints.
    /// </summary>
    FullCipherAccess UnrestrictedForWholeVaultExport();
}
