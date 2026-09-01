using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// The decision point for PAM credential leasing in Vault code. A cipher reachable only through
/// leasing-enabled collections — those governed by an access rule that is currently <em>enabled</em> — is
/// "leasing-gated": its secrets are withheld (partial data) unless the caller holds a valid active lease,
/// and mutating it is refused without one. A collection whose rule has been switched off gates nothing.
/// Every method is "unrestricted" when the <c>Pam</c> feature flag is off, so flag-off behaviour is
/// unchanged.
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
    /// The caller's collections, loaded through a collection read path so that
    /// <see cref="CollectionDetails.HasEnabledAccessRule"/> is populated — the implementation reads it to
    /// tell a governing rule that is switched on from one that is not. <c>null</c> means "not loaded,
    /// because the caller has no organizations" and is equivalent to empty: with no collection to reach a
    /// cipher through, nothing is gated.
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
    /// Read decision for a <em>write-return</em>: the response echoing back a cipher the caller has just
    /// mutated. Returns a witness authorizing full data only when <paramref name="cipher"/> is not gated; a
    /// gated cipher yields <c>null</c> whatever lease the caller holds.
    /// </summary>
    /// <remarks>
    /// Stricter than <see cref="AuthorizeReadAsync"/>, for the same reason the bulk read is strict: a client
    /// persists a write-return into its local store, so the copy outlives the lease that justified it. The
    /// caller submitted the mutation and therefore already holds what it sent, which makes the echo a
    /// round-trip saving rather than something correctness rests on. Full secrets for a gated cipher are
    /// released only by an explicit single-cipher read.
    ///
    /// Like the read decisions this only ever decides, and never throws. What a caller does with a null
    /// witness is its own call: a client that cannot render the reduced shape has the cipher withheld
    /// entirely rather than reduced, which for a write-return means reporting not-found for a mutation that
    /// was applied (see <see cref="Vault.Authorization.PartialCipherSupport"/>).
    /// </remarks>
    Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Administrative counterpart of <see cref="AuthorizeWriteReturnAsync"/>, resolving leasing status from
    /// the organization's collections rather than the caller's, as the other "/admin" decisions do.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(Guid userId, Guid organizationId, Cipher cipher);

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
    /// store lives; a bulk write's <em>request</em> copies no secret anywhere, so refusing a lease-holder's
    /// own edit would withhold nothing and only break the feature for the person the lease was issued to.
    /// What a write <em>returns</em> is a separate decision, and a strict one — see
    /// <see cref="AuthorizeWriteReturnAsync"/>.
    /// </remarks>
    Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Per-cipher read for a caller reaching the cipher through organization-wide permission rather than
    /// through their collection assignments — the "/admin" endpoints.
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
