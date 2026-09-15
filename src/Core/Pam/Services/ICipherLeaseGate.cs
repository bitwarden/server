using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// The decision point for PAM credential leasing. A leasing-gated cipher withholds secrets without a
/// valid active lease.
/// </summary>
/// <remarks>
/// Members resolve leasing from their own collections; administrators resolve it from the organization's.
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
    /// Bulk counterpart of <see cref="AuthorizeReadAsync"/>; authorizes only the non-gated subset,
    /// computed in-memory. Gated ciphers are always stripped, since secrets release one at a time.
    /// </summary>
    /// <param name="collections">
    /// The caller's collections, loaded so <see cref="CollectionDetails.HasEnabledAccessRule"/> is
    /// populated. <c>null</c> is equivalent to empty (the caller has no organizations).
    /// </param>
    /// <param name="collectionCiphersByCipher">
    /// The caller's cipher-to-collection mappings. A cipher absent from the dictionary is reachable
    /// through no collection and so is not gated.
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
    /// Per-cipher write decision; throws <see cref="NotFoundException"/> when mutation is refused
    /// (gated, no valid lease).
    /// </summary>
    /// <remarks>
    /// <see cref="NotFoundException"/>, not forbidden, deliberately: a member who cannot reach a
    /// credential should not learn from a write attempt that it exists.
    /// </remarks>
    Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk write decision covering all ciphers; refuses the batch if any is gated with no valid lease.
    /// </summary>
    /// <remarks>
    /// A held lease widens this beyond the strict bulk read, since a write copies no secret anywhere.
    /// What a write <em>returns</em> stays strict — see <see cref="AuthorizeWriteReturnAsync"/>.
    /// </remarks>
    Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Per-cipher read via organization-wide permission rather than collection assignments — the "/admin" endpoints.
    /// </summary>
    /// <remarks>
    /// Leasing status is resolved from the organization's collections, not the caller's.
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
