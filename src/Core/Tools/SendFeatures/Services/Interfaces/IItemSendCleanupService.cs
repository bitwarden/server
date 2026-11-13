namespace Bit.Core.Tools.Services;

/// <summary>
/// Deletes item-type Sends, from any owner, that reference vault items (ciphers) which are being removed.
/// </summary>
public interface IItemSendCleanupService
{
    /// <summary>
    /// Deletes the item-type Sends that reference the given ciphers. Call only for ciphers that are actually
    /// being removed; a Send whose item still exists is left alone.
    /// </summary>
    /// <param name="cipherIds">Identifies the ciphers being removed.</param>
    Task DeleteItemSendsForCiphersAsync(IEnumerable<Guid> cipherIds);

    /// <summary>
    /// Deletes the item-type Sends that reference any cipher owned by the organization. Call before the
    /// organization's ciphers are removed, because the cipher IDs are read from storage here.
    /// </summary>
    /// <param name="organizationId">Identifies the organization being deleted.</param>
    Task DeleteItemSendsForOrganizationAsync(Guid organizationId);
}
