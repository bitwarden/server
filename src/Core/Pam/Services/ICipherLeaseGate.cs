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
    /// Per-cipher read through the caller's own collections. Returns a <see cref="FullCipherAccess"/> witness when
    /// the cipher is not gated or the caller holds a valid active lease, otherwise <c>null</c> for the partial shape.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk counterpart of <see cref="AuthorizeReadAsync"/> that strips every gated cipher; secrets release one at a
    /// time.
    /// </summary>
    /// <param name="collections">
    /// Must have <see cref="CollectionDetails.HasEnabledAccessRule"/> populated; <c>null</c> means none.
    /// </param>
    /// <param name="collectionCiphersByCipher">A cipher missing here is in no collection, so not gated.</param>
    Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher);

    /// <summary>
    /// The bulk member decision, loading the caller's collections and mappings itself, once and only while the flag
    /// is on.
    /// </summary>
    Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Read decision for the response echoing a cipher the caller just mutated. Any gated cipher yields <c>null</c>
    /// whatever lease the caller holds, because the client persists the echo beyond the lease.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Administrative counterpart of <see cref="AuthorizeWriteReturnAsync"/>, resolving leasing status from
    /// the organization's collections rather than the caller's, as the other "/admin" decisions do.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(Guid userId, Guid organizationId, Cipher cipher);

    /// <summary>
    /// Per-cipher write decision. Throws <see cref="NotFoundException"/> for a gated cipher without a valid lease, so
    /// a write attempt does not reveal that the credential exists.
    /// </summary>
    Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher);

    /// <summary>
    /// Bulk write decision; refuses the batch if any cipher is gated without a valid lease. Unlike the bulk read, a
    /// held lease suffices, since a write copies no secret anywhere.
    /// </summary>
    Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Per-cipher read for the "/admin" endpoints, resolving leasing from the organization's collections rather than
    /// the caller's.
    /// </summary>
    Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher);

    /// <summary>
    /// Bulk counterpart of <see cref="AuthorizeAdminReadAsync"/>, stripping every gated cipher regardless
    /// of lease state, as the member bulk decision does. Loads the organization's leasing-enabled
    /// collections once, and only when the flag is on.
    /// </summary>
    Task<FullCipherAccess> AuthorizeAdminReadManyAsync(
        Guid userId,
        Guid organizationId,
        IEnumerable<Cipher> ciphers);

    /// <summary>
    /// Mints an unrestricted witness for whole-vault organization export, the only context where leasing is waived,
    /// since a partially stripped export is not a usable backup. The caller must establish export permission first.
    /// </summary>
    FullCipherAccess UnrestrictedForWholeVaultExport();
}
