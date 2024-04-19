using Bit.Core.Entities;

namespace Bit.Core.Vault.Models.Data;

public class CipherDetails : CipherOrganizationDetails
{
    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public bool Edit { get; set; }
    public bool ViewPassword { get; set; }

    public CipherDetails() { }

    public CipherDetails(CipherOrganizationDetails cipher)
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
    }
}

public class CipherDetailsWithCollections : CipherDetails
{
    public CipherDetailsWithCollections(
        CipherDetails cipher,
        Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict)
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
        FolderId = cipher.FolderId;
        Favorite = cipher.Favorite;
        Edit = cipher.Edit;
        ViewPassword = cipher.ViewPassword;

        CollectionIds = collectionCiphersGroupDict.TryGetValue(Id, out var value)
            ? value.Select(cc => cc.CollectionId)
            : Array.Empty<Guid>();
    }

    public CipherDetailsWithCollections(CipherOrganizationDetails cipher, Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict)
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

        CollectionIds = collectionCiphersGroupDict != null && collectionCiphersGroupDict.TryGetValue(Id, out var value)
            ? value.Select(cc => cc.CollectionId)
            : Array.Empty<Guid>();
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}
