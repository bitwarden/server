

using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Settings;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;
// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

public class CipherMiniResponseModel : ResponseModel
{
    // PARTIAL/safe constructor. Under PAM credential leasing the secret Data blob is never emitted from
    // here — only the reduced PartialData. Any path that uses this type without a FullCipherAccess witness
    // therefore fails closed: a missed migration returns partial data (a visible bug), never a leak.
    public CipherMiniResponseModel(Cipher cipher, IGlobalSettings globalSettings, bool orgUseTotp,
        string obj = "cipherMini")
        : this(cipher, globalSettings, orgUseTotp, obj, partial: true)
    {
    }

    // Shared construction. When partial is false the secret Data is left null for a derived Full* type to
    // populate via PopulateFullData; this constructor never emits secret data on its own.
    protected CipherMiniResponseModel(Cipher cipher, IGlobalSettings globalSettings, bool orgUseTotp,
        string obj, bool partial)
        : base(obj)
    {
        if (cipher == null)
        {
            throw new ArgumentNullException(nameof(cipher));
        }

        Id = cipher.Id;
        Type = cipher.Type;
        RevisionDate = cipher.RevisionDate;
        OrganizationId = cipher.OrganizationId;
        Attachments = AttachmentResponseModel.FromCipher(cipher, globalSettings);
        OrganizationUseTotp = orgUseTotp;
        CreationDate = cipher.CreationDate;
        DeletedDate = cipher.DeletedDate;
        Reprompt = cipher.Reprompt.GetValueOrDefault(CipherRepromptType.None);
        Key = cipher.Key;

        if (partial && !cipher.IsDataBlobEncrypted())
        {
            // The reduced blob signals the cipher is leasing-gated; the client decrypts PartialData itself.
            // An opaque (SDK-encrypted) blob can't be reshaped without decrypting, so nothing is returned.
            PartialData = PartialCipherData.Strip(cipher.Type, cipher.Data);
        }
    }

    /// <summary>
    /// Populates the full secret data blob (and the obsolete typed fields) for a Full* response. Requires a
    /// <see cref="FullCipherAccess"/> witness authorizing this cipher, so full secret data cannot be emitted
    /// without first passing through the leasing gate that mints the witness.
    /// </summary>
    protected void PopulateFullData(FullCipherAccess access, Cipher cipher)
    {
        access.Require(cipher.Id);

        Data = cipher.Data;

        if (cipher.IsDataBlobEncrypted())
        {
            return;
        }

        CipherData cipherData;
        switch (cipher.Type)
        {
            case CipherType.Login:
                var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
                cipherData = loginData;
                Login = new CipherLoginModel(loginData);
                break;
            case CipherType.SecureNote:
                var secureNoteData = JsonSerializer.Deserialize<CipherSecureNoteData>(cipher.Data);
                cipherData = secureNoteData;
                SecureNote = new CipherSecureNoteModel(secureNoteData);
                break;
            case CipherType.Card:
                var cardData = JsonSerializer.Deserialize<CipherCardData>(cipher.Data);
                cipherData = cardData;
                Card = new CipherCardModel(cardData);
                break;
            case CipherType.Identity:
                var identityData = JsonSerializer.Deserialize<CipherIdentityData>(cipher.Data);
                cipherData = identityData;
                Identity = new CipherIdentityModel(identityData);
                break;
            case CipherType.SSHKey:
                var sshKeyData = JsonSerializer.Deserialize<CipherSSHKeyData>(cipher.Data);
                cipherData = sshKeyData;
                SSHKey = new CipherSSHKeyModel(sshKeyData);
                break;
            case CipherType.BankAccount:
                var bankAccountData = JsonSerializer.Deserialize<CipherBankAccountData>(cipher.Data);
                cipherData = bankAccountData;
                BankAccount = new CipherBankAccountModel(bankAccountData);
                break;
            case CipherType.DriversLicense:
                var driversLicenseData = JsonSerializer.Deserialize<CipherDriversLicenseData>(cipher.Data);
                cipherData = driversLicenseData;
                DriversLicense = new CipherDriversLicenseModel(driversLicenseData);
                break;
            case CipherType.Passport:
                var passportData = JsonSerializer.Deserialize<CipherPassportData>(cipher.Data);
                cipherData = passportData;
                Passport = new CipherPassportModel(passportData);
                break;
            default:
                throw new ArgumentException("Unsupported " + nameof(Type) + ".");
        }

        Name = cipherData.Name;
        Notes = cipherData.Notes;
        Fields = cipherData.Fields?.Select(f => new CipherFieldModel(f));
        PasswordHistory = cipherData.PasswordHistory?.Select(ph => new CipherPasswordHistoryModel(ph));
    }

    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public CipherType Type { get; set; }

    // Setter is locked so the secret blob can only ever be populated through the witness-gated
    // PopulateFullData path, never via a public constructor or object initializer.
    public string Data { get; protected set; }

    /// <summary>
    /// The reduced data blob returned in place of <see cref="Data"/> when the caller can only reach this
    /// cipher through leasing-enabled collections (PAM credential leasing). Contains the encrypted title
    /// and, for logins, the encrypted URIs — never the dropped secrets. Null for full responses.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string PartialData { get; set; }

    [Obsolete("Use Data instead.")]
    public string Name { get; protected set; }

    [Obsolete("Use Data instead.")]
    public string Notes { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherLoginModel Login { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherCardModel Card { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherIdentityModel Identity { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherSecureNoteModel SecureNote { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherSSHKeyModel SSHKey { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherBankAccountModel BankAccount { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherDriversLicenseModel DriversLicense { get; protected set; }

    [Obsolete("Use Data instead.")]
    public CipherPassportModel Passport { get; protected set; }

    [Obsolete("Use Data instead.")]
    public IEnumerable<CipherFieldModel> Fields { get; protected set; }

    [Obsolete("Use Data instead.")]
    public IEnumerable<CipherPasswordHistoryModel> PasswordHistory { get; protected set; }
    public IEnumerable<AttachmentResponseModel> Attachments { get; set; }
    public bool OrganizationUseTotp { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public CipherRepromptType Reprompt { get; set; }
    public string Key { get; set; }
}

/// <summary>
/// The full-data counterpart of <see cref="CipherMiniResponseModel"/>. Its constructor requires a
/// <see cref="FullCipherAccess"/> witness (minted only by the leasing gate), making emission of full
/// secret data a deliberate, type-checked act.
/// </summary>
public class FullCipherMiniResponseModel : CipherMiniResponseModel
{
    public FullCipherMiniResponseModel(FullCipherAccess access, Cipher cipher, IGlobalSettings globalSettings,
        bool orgUseTotp, string obj = "cipherMini")
        : base(cipher, globalSettings, orgUseTotp, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }
}

#nullable enable
public class CipherResponseModel : CipherMiniResponseModel
{
    public CipherResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IGlobalSettings globalSettings,
        string obj = "cipher")
        : this(cipher, user, organizationAbility, globalSettings, obj, partial: true)
    {
    }

    protected CipherResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IGlobalSettings globalSettings,
        string obj,
        bool partial)
        : base(cipher, globalSettings, cipher.OrganizationUseTotp, obj, partial)
    {
        FolderId = cipher.FolderId;
        Favorite = cipher.Favorite;
        Edit = cipher.Edit;
        ArchivedDate = cipher.ArchivedDate;
        ViewPassword = cipher.ViewPassword;
        Permissions = new CipherPermissionsResponseModel(user, cipher, organizationAbility);
    }

    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public bool Edit { get; set; }
    public bool ViewPassword { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public CipherPermissionsResponseModel Permissions { get; set; }
}

/// <summary>Full-data counterpart of <see cref="CipherResponseModel"/>; requires a gate-minted witness.</summary>
public class FullCipherResponseModel : CipherResponseModel
{
    public FullCipherResponseModel(
        FullCipherAccess access,
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IGlobalSettings globalSettings,
        string obj = "cipher")
        : base(cipher, user, organizationAbility, globalSettings, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }
}

public class CipherDetailsResponseModel : CipherResponseModel
{
    public CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherDetails")
        : this(cipher, user, organizationAbility, globalSettings, collectionCiphers, obj, partial: true)
    {
    }

    protected CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj, bool partial)
        : base(cipher, user, organizationAbility, globalSettings, obj, partial)
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
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails")
        : this(cipher, user, organizationAbility, globalSettings, collectionCiphers, obj, partial: true)
    {
    }

    protected CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj, bool partial)
        : base(cipher, user, organizationAbility, globalSettings, obj, partial)
    {
        CollectionIds = collectionCiphers?.Select(c => c.CollectionId) ?? [];
    }

    public CipherDetailsResponseModel(
        CipherDetailsWithCollections cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        string obj = "cipherDetails")
        : this(cipher, user, organizationAbility, globalSettings, obj, partial: true)
    {
    }

    protected CipherDetailsResponseModel(
        CipherDetailsWithCollections cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        string obj, bool partial)
        : base(cipher, user, organizationAbility, globalSettings, obj, partial)
    {
        CollectionIds = cipher.CollectionIds ?? [];
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}

/// <summary>Full-data counterpart of <see cref="CipherDetailsResponseModel"/>; requires a gate-minted witness.</summary>
public class FullCipherDetailsResponseModel : CipherDetailsResponseModel
{
    public FullCipherDetailsResponseModel(
        FullCipherAccess access,
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, globalSettings, collectionCiphers, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }

    public FullCipherDetailsResponseModel(
        FullCipherAccess access,
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, globalSettings, collectionCiphers, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }

    public FullCipherDetailsResponseModel(
        FullCipherAccess access,
        CipherDetailsWithCollections cipher,
        User user,
        OrganizationAbility? organizationAbility,
        GlobalSettings globalSettings,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, globalSettings, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }
}

public class CipherMiniDetailsResponseModel : CipherMiniResponseModel
{
    public CipherMiniDetailsResponseModel(Cipher cipher, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj = "cipherMiniDetails")
        : this(cipher, globalSettings, collectionCiphers, orgUseTotp, obj, partial: true)
    {
    }

    protected CipherMiniDetailsResponseModel(Cipher cipher, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj, bool partial)
        : base(cipher, globalSettings, orgUseTotp, obj, partial)
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
        : this(cipher, globalSettings, orgUseTotp, obj, partial: true)
    {
    }

    protected CipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        GlobalSettings globalSettings, bool orgUseTotp, string obj, bool partial)
        : base(cipher, globalSettings, orgUseTotp, obj, partial)
    {
        CollectionIds = cipher.CollectionIds ?? new List<Guid>();
    }

    public CipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        GlobalSettings globalSettings, string obj = "cipherMiniDetails")
        : this(cipher, globalSettings, cipher.OrganizationUseTotp, obj, partial: true)
    {
    }

    public IEnumerable<Guid> CollectionIds { get; set; }
}

/// <summary>Full-data counterpart of <see cref="CipherMiniDetailsResponseModel"/>; requires a gate-minted witness.</summary>
public class FullCipherMiniDetailsResponseModel : CipherMiniDetailsResponseModel
{
    public FullCipherMiniDetailsResponseModel(FullCipherAccess access, Cipher cipher, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, collectionCiphers, orgUseTotp, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }

    public FullCipherMiniDetailsResponseModel(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings, bool orgUseTotp,
        string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, orgUseTotp, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }

    public FullCipherMiniDetailsResponseModel(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings,
        string obj = "cipherMiniDetails")
        : base(cipher, globalSettings, cipher.OrganizationUseTotp, obj, partial: false)
    {
        PopulateFullData(access, cipher);
    }
}
