using Bit.Core.Entities;
using Bit.Core.Models.Data;

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
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict)
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

    // TODO: clean up all these different cipher models and ctors
    public CipherDetailsWithCollections(CipherOrganizationDetails cipher, IEnumerable<Guid> collectionIds)
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

        CollectionIds = collectionIds;
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}

public class EnrichedCipherDetails : CipherDetailsWithCollections
{
    public EnrichedCipherDetails(CipherDetailsWithCollections cipher, ItemPermissions permissions) : base(cipher, cipher.CollectionIds)
    {
        Permissions = permissions;
    }

    public ItemPermissions Permissions { get; set; }
}
