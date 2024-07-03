namespace Bit.Api.Vault.Models.Request;

public class CipherBulkUpdateCollectionsRequestModel
{
    public Guid OrganizationId { get; set; }

    public IEnumerable<Guid> CipherIds { get; set; }

    public IEnumerable<Guid> CollectionIds { get; set; }

    /// <summary>
    /// If true, the collections will be removed from the ciphers. Otherwise, they will be added.
    /// </summary>
    public bool RemoveCollections { get; set; }
}
