// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public class CipherMiniResponseModel : ResponseModel
{
    public CipherMiniResponseModel(Cipher cipher, IGlobalSettings globalSettings, bool orgUseTotp, string obj = "cipherMini")
        : base(obj)
    {
        if (cipher == null)
        {
            throw new ArgumentNullException(nameof(cipher));
        }

        Id = cipher.Id;
        Type = cipher.Type;

        CipherData cipherData;
        switch (cipher.Type)
        {
            case CipherType.Login:
                var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
                cipherData = loginData;
                Data = loginData;
                Login = new CipherLoginModel(loginData);
                break;
            case CipherType.SecureNote:
                var secureNoteData = JsonSerializer.Deserialize<CipherSecureNoteData>(cipher.Data);
                Data = secureNoteData;
                cipherData = secureNoteData;
                SecureNote = new CipherSecureNoteModel(secureNoteData);
                break;
            case CipherType.Card:
                var cardData = JsonSerializer.Deserialize<CipherCardData>(cipher.Data);
                Data = cardData;
                cipherData = cardData;
                Card = new CipherCardModel(cardData);
                break;
            case CipherType.Identity:
                var identityData = JsonSerializer.Deserialize<CipherIdentityData>(cipher.Data);
                Data = identityData;
                cipherData = identityData;
                Identity = new CipherIdentityModel(identityData);
                break;
            case CipherType.SSHKey:
                var sshKeyData = JsonSerializer.Deserialize<CipherSSHKeyData>(cipher.Data);
                Data = sshKeyData;
                cipherData = sshKeyData;
                SSHKey = new CipherSSHKeyModel(sshKeyData);
                break;
            default:
                throw new ArgumentException("Unsupported " + nameof(Type) + ".");
        }

        Name = cipherData.Name;
        Notes = cipherData.Notes;
        Fields = cipherData.Fields?.Select(f => new CipherFieldModel(f));
        PasswordHistory = cipherData.PasswordHistory?.Select(ph => new CipherPasswordHistoryModel(ph));
        RevisionDate = cipher.RevisionDate;
        OrganizationId = cipher.OrganizationId;
        Attachments = AttachmentResponseModel.FromCipher(cipher, globalSettings);
        OrganizationUseTotp = orgUseTotp;
        CreationDate = cipher.CreationDate;
        DeletedDate = cipher.DeletedDate;
        Reprompt = cipher.Reprompt.GetValueOrDefault(CipherRepromptType.None);
        Key = cipher.Key;
        ArchivedDate = cipher.ArchivedDate;
    }

    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public CipherType Type { get; set; }
    public dynamic Data { get; set; }
    public string Name { get; set; }
    public string Notes { get; set; }

    [Obsolete("This property is deprecated and will be removed in an upcoming release.")]
    public CipherLoginModel Login { get; set; }

    [Obsolete("This property is deprecated and will be removed in an upcoming release.")]
    public CipherCardModel Card { get; set; }

    [Obsolete("This property is deprecated and will be removed in an upcoming release.")]
    public CipherIdentityModel Identity { get; set; }

    [Obsolete("This property is deprecated and will be removed in an upcoming release.")]
    public CipherSecureNoteModel SecureNote { get; set; }

    [Obsolete("This property is deprecated and will be removed in an upcoming release.")]
    public CipherSSHKeyModel SSHKey { get; set; }
    public IEnumerable<CipherFieldModel> Fields { get; set; }
    public IEnumerable<CipherPasswordHistoryModel> PasswordHistory { get; set; }
    public IEnumerable<AttachmentResponseModel> Attachments { get; set; }
    public bool OrganizationUseTotp { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public CipherRepromptType Reprompt { get; set; }
    public string Key { get; set; }
    public DateTime? ArchivedDate { get; set; }
}

public class CipherResponseModel : CipherMiniResponseModel
{
    public CipherResponseModel(
        CipherDetails cipher,
        User user,
        IDictionary<Guid, OrganizationAbility> organizationAbilities,
        IGlobalSettings globalSettings,
        string obj = "cipher")
        : base(cipher, globalSettings, cipher.OrganizationUseTotp, obj)
    {
        FolderId = cipher.FolderId;
        Favorite = cipher.Favorite;
        Edit = cipher.Edit;
        ViewPassword = cipher.ViewPassword;
        Permissions = new CipherPermissionsResponseModel(user, cipher, organizationAbilities);
    }

    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public bool Edit { get; set; }
    public bool ViewPassword { get; set; }
    public CipherPermissionsResponseModel Permissions { get; set; }
}

public class CipherDetailsResponseModel : CipherResponseModel
{
    public CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        IDictionary<Guid, OrganizationAbility> organizationAbilities,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherDetails")
        : base(cipher, user, organizationAbilities, globalSettings, obj)
    {
        if (collectionCiphers?.TryGetValue(cipher.Id, out var collectionCipher) ?? false)
        {
            CollectionIds = collectionCipher.Select(c => c.CollectionId);
        }
        else
        {
            CollectionIds = [];
        }
    }

    public CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        IDictionary<Guid, OrganizationAbility> organizationAbilities,
        GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails")
        : base(cipher, user, organizationAbilities, globalSettings, obj)
    {
        CollectionIds = collectionCiphers?.Select(c => c.CollectionId) ?? [];
    }

    public CipherDetailsResponseModel(
        CipherDetailsWithCollections cipher,
        User user,
        IDictionary<Guid, OrganizationAbility> organizationAbilities,
        GlobalSettings globalSettings,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbilities, globalSettings, obj)
    {
        CollectionIds = cipher.CollectionIds ?? [];
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}

public class CipherMiniDetailsResponseModel : CipherMiniResponseModel
{
    public CipherMiniDetailsResponseModel(Cipher cipher, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp, string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, orgUseTotp, obj)
    {
        if (collectionCiphers?.TryGetValue(cipher.Id, out var collectionCipher) ?? false)
        {
            CollectionIds = collectionCipher.Select(c => c.CollectionId);
        }
        else
        {
            CollectionIds = [];
        }
    }

    public CipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        GlobalSettings globalSettings, bool orgUseTotp, string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, orgUseTotp, obj)
    {
        CollectionIds = cipher.CollectionIds ?? [];
    }

    public CipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        GlobalSettings globalSettings, string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, cipher.OrganizationUseTotp, obj)
    {
        CollectionIds = cipher.CollectionIds ?? new List<Guid>();
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}
