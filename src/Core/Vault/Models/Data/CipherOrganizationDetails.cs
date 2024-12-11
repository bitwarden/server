using Bit.Core.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Models.Data;

public class CipherOrganizationDetails : Cipher
{
    public bool OrganizationUseTotp { get; set; }
}

public class CipherOrganizationDetailsWithCollections : CipherOrganizationDetails
{
    public CipherOrganizationDetailsWithCollections(
        CipherOrganizationDetails cipher,
        Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict
    )
    {
        Id = cipher.Id;
        UserId = cipher.UserId;
        OrganizationId = cipher.OrganizationId;
        Type = cipher.Type;
        Data = cipher.Data;
        Favorites = cipher.Favorites;
        Folders = cipher.Folders;
        Attachments = cipher.Attachments;
        CreationDate = cipher.CreationDate;
        RevisionDate = cipher.RevisionDate;
        DeletedDate = cipher.DeletedDate;
        Reprompt = cipher.Reprompt;
        Key = cipher.Key;
        OrganizationUseTotp = cipher.OrganizationUseTotp;

        CollectionIds = collectionCiphersGroupDict.TryGetValue(Id, out var value)
            ? value.Select(cc => cc.CollectionId)
            : Array.Empty<Guid>();
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}
