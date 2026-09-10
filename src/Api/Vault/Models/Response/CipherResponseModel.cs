

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

/// <summary>
/// The shape shared by every cipher response. Abstract because a response has to say which data it
/// carries: a <c>Partial*</c> subclass emits the reduced <see cref="PartialData"/> envelope for a
/// leasing-gated cipher, and a <c>Full*</c> subclass emits the secret <see cref="Data"/> blob and can
/// only be constructed with a <see cref="FullCipherAccess"/> witness. Both properties are declared here
/// so either shape serializes to the same contract.
/// </summary>
public abstract class CipherMiniResponseModel : ResponseModel
{
    protected CipherMiniResponseModel(Cipher cipher, bool orgUseTotp, string obj)
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
        OrganizationUseTotp = orgUseTotp;
        CreationDate = cipher.CreationDate;
        DeletedDate = cipher.DeletedDate;
        Reprompt = cipher.Reprompt.GetValueOrDefault(CipherRepromptType.None);
        Key = cipher.Key;
    }

    /// <summary>
    /// Builds the shape <paramref name="access"/> permits: full when it authorizes this cipher, reduced
    /// otherwise — including when there is no witness at all.
    /// </summary>
    /// <remarks>
    /// This is the only place the authorization test is written for a response shape. Use it wherever the
    /// shape depends on the witness; a path authorized out of band constructs its <c>Full*</c> directly,
    /// which keeps that decision visible at the call site.
    /// </remarks>
    public static CipherMiniResponseModel From(FullCipherAccess access, Cipher cipher,
        IGlobalSettings globalSettings, bool orgUseTotp, string obj = "cipherMini") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherMiniResponseModel(access, cipher, globalSettings, orgUseTotp, obj)
            : new PartialCipherMiniResponseModel(cipher, orgUseTotp, obj);

    /// <summary>
    /// Populates the reduced data blob for a <c>Partial*</c> response. Attachment metadata is left unset:
    /// it carries each attachment's encryption key, and the leasing gate also blocks the attachment
    /// download, so nothing about a withheld attachment is exposed.
    /// </summary>
    /// <remarks>
    /// An opaque (SDK-encrypted) blob cannot be reshaped without decrypting it, so nothing is returned
    /// for one. That combination is unreachable: only a cipher reached through leasing-enabled
    /// collections is gated, which makes it organization-owned, and organization items are never blob
    /// encrypted.
    /// </remarks>
    protected void PopulatePartialData(Cipher cipher)
    {
        if (cipher.IsDataBlobEncrypted())
        {
            return;
        }

        PartialData = PartialCipherData.Strip(cipher.Type, cipher.Data);
    }

    /// <summary>
    /// Populates the full secret data blob, the attachment metadata, and the obsolete typed fields for a
    /// <c>Full*</c> response. Requires a <see cref="FullCipherAccess"/> witness authorizing this cipher,
    /// so full secret data cannot be emitted without first passing through the leasing gate that mints
    /// the witness.
    /// </summary>
    protected void PopulateFullData(FullCipherAccess access, Cipher cipher, IGlobalSettings globalSettings)
    {
        ArgumentNullException.ThrowIfNull(access);
        access.Require(cipher.Id);

        Attachments = AttachmentResponseModel.FromCipher(cipher, globalSettings);
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
    public string PartialData { get; protected set; }

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
    public IEnumerable<AttachmentResponseModel> Attachments { get; protected set; }
    public bool OrganizationUseTotp { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public CipherRepromptType Reprompt { get; set; }
    public string Key { get; set; }
}

/// <summary>
/// A <see cref="CipherMiniResponseModel"/> carrying only the reduced <c>PartialData</c> envelope, for a
/// leasing-gated cipher the caller holds no valid active lease for. Takes no global settings because a
/// partial response emits no attachment metadata.
/// </summary>
public sealed class PartialCipherMiniResponseModel : CipherMiniResponseModel
{
    public PartialCipherMiniResponseModel(Cipher cipher, bool orgUseTotp, string obj = "cipherMini")
        : base(cipher, orgUseTotp, obj)
    {
        PopulatePartialData(cipher);
    }
}

/// <summary>
/// A <see cref="CipherMiniResponseModel"/> carrying the cipher's full secret data. Constructing one
/// requires a <see cref="FullCipherAccess"/> witness authorizing the cipher, so secret data can only be
/// emitted by a path that has passed through the leasing gate.
/// </summary>
public sealed class FullCipherMiniResponseModel : CipherMiniResponseModel
{
    public FullCipherMiniResponseModel(FullCipherAccess access, Cipher cipher,
        IGlobalSettings globalSettings, bool orgUseTotp, string obj = "cipherMini")
        : base(cipher, orgUseTotp, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }
}
#nullable enable
public abstract class CipherResponseModel : CipherMiniResponseModel
{
    protected CipherResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        string obj)
        : base(cipher, cipher.OrganizationUseTotp, obj)
    {
        FolderId = cipher.FolderId;
        Favorite = cipher.Favorite;
        Edit = cipher.Edit;
        ArchivedDate = cipher.ArchivedDate;
        ViewPassword = cipher.ViewPassword;
        Permissions = new CipherPermissionsResponseModel(user, cipher, organizationAbility);
    }

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherResponseModel From(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, IGlobalSettings globalSettings, string obj = "cipher") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherResponseModel(access, cipher, user, organizationAbility, globalSettings, obj)
            : new PartialCipherResponseModel(cipher, user, organizationAbility, obj);

    public Guid? FolderId { get; set; }
    public bool Favorite { get; set; }
    public bool Edit { get; set; }
    public bool ViewPassword { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public CipherPermissionsResponseModel Permissions { get; set; }
}

/// <summary>The reduced-data counterpart of <see cref="CipherResponseModel"/>.</summary>
public sealed class PartialCipherResponseModel : CipherResponseModel
{
    public PartialCipherResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        string obj = "cipher")
        : base(cipher, user, organizationAbility, obj)
    {
        PopulatePartialData(cipher);
    }
}

/// <summary>The full-data counterpart of <see cref="CipherResponseModel"/>.</summary>
public sealed class FullCipherResponseModel : CipherResponseModel
{
    public FullCipherResponseModel(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, IGlobalSettings globalSettings, string obj = "cipher")
        : base(cipher, user, organizationAbility, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }
}

public abstract class CipherDetailsResponseModel : CipherResponseModel
{
    protected CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers,
        string obj)
        : base(cipher, user, organizationAbility, obj)
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

    protected CipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IEnumerable<CollectionCipher> collectionCiphers,
        string obj)
        : base(cipher, user, organizationAbility, obj)
    {
        CollectionIds = collectionCiphers?.Select(c => c.CollectionId) ?? [];
    }

    protected CipherDetailsResponseModel(
        CipherDetailsWithCollections cipher,
        User user,
        OrganizationAbility? organizationAbility,
        string obj)
        : base(cipher, user, organizationAbility, obj)
    {
        CollectionIds = cipher.CollectionIds ?? [];
    }

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherDetailsResponseModel From(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers,
        string obj = "cipherDetails") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherDetailsResponseModel(access, cipher, user, organizationAbility, globalSettings, collectionCiphers, obj)
            : new PartialCipherDetailsResponseModel(cipher, user, organizationAbility, collectionCiphers, obj);

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherDetailsResponseModel From(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherDetailsResponseModel(access, cipher, user, organizationAbility, globalSettings, collectionCiphers, obj)
            : new PartialCipherDetailsResponseModel(cipher, user, organizationAbility, collectionCiphers, obj);

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherDetailsResponseModel From(FullCipherAccess access, CipherDetailsWithCollections cipher,
        User user, OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        string obj = "cipherDetails") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherDetailsResponseModel(access, cipher, user, organizationAbility, globalSettings, obj)
            : new PartialCipherDetailsResponseModel(cipher, user, organizationAbility, obj);

    public IEnumerable<Guid> CollectionIds { get; set; }
}

/// <summary>The reduced-data counterpart of <see cref="CipherDetailsResponseModel"/>.</summary>
public sealed class PartialCipherDetailsResponseModel : CipherDetailsResponseModel
{
    public PartialCipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, collectionCiphers, obj)
    {
        PopulatePartialData(cipher);
    }

    public PartialCipherDetailsResponseModel(
        CipherDetails cipher,
        User user,
        OrganizationAbility? organizationAbility,
        IEnumerable<CollectionCipher> collectionCiphers,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, collectionCiphers, obj)
    {
        PopulatePartialData(cipher);
    }

    public PartialCipherDetailsResponseModel(
        CipherDetailsWithCollections cipher,
        User user,
        OrganizationAbility? organizationAbility,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, obj)
    {
        PopulatePartialData(cipher);
    }
}

/// <summary>The full-data counterpart of <see cref="CipherDetailsResponseModel"/>.</summary>
public sealed class FullCipherDetailsResponseModel : CipherDetailsResponseModel
{
    public FullCipherDetailsResponseModel(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, collectionCiphers, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }

    public FullCipherDetailsResponseModel(FullCipherAccess access, CipherDetails cipher, User user,
        OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, collectionCiphers, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }

    public FullCipherDetailsResponseModel(FullCipherAccess access, CipherDetailsWithCollections cipher,
        User user, OrganizationAbility? organizationAbility, GlobalSettings globalSettings,
        string obj = "cipherDetails")
        : base(cipher, user, organizationAbility, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }
}

public abstract class CipherMiniDetailsResponseModel : CipherMiniResponseModel
{
    protected CipherMiniDetailsResponseModel(Cipher cipher,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp, string obj)
        : base(cipher, orgUseTotp, obj)
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

    protected CipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        bool orgUseTotp, string obj)
        : base(cipher, orgUseTotp, obj)
    {
        CollectionIds = cipher.CollectionIds ?? [];
    }

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherMiniDetailsResponseModel From(FullCipherAccess access, Cipher cipher,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj = "cipherMiniDetails") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherMiniDetailsResponseModel(access, cipher, globalSettings, collectionCiphers, orgUseTotp, obj)
            : new PartialCipherMiniDetailsResponseModel(cipher, collectionCiphers, orgUseTotp, obj);

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherMiniDetailsResponseModel From(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings, bool orgUseTotp,
        string obj = "cipherMiniDetails") =>
        access?.Authorizes(cipher.Id) == true
            ? new FullCipherMiniDetailsResponseModel(access, cipher, globalSettings, orgUseTotp, obj)
            : new PartialCipherMiniDetailsResponseModel(cipher, orgUseTotp, obj);

    /// <inheritdoc cref="CipherMiniResponseModel.From(FullCipherAccess, Cipher, IGlobalSettings, bool, string)"/>
    public static CipherMiniDetailsResponseModel From(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings,
        string obj = "cipherMiniDetails") =>
        From(access, cipher, globalSettings, cipher.OrganizationUseTotp, obj);

    public IEnumerable<Guid> CollectionIds { get; set; }
}

/// <summary>The reduced-data counterpart of <see cref="CipherMiniDetailsResponseModel"/>.</summary>
public sealed class PartialCipherMiniDetailsResponseModel : CipherMiniDetailsResponseModel
{
    public PartialCipherMiniDetailsResponseModel(Cipher cipher,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj = "cipherMiniDetails")
        : base(cipher, collectionCiphers, orgUseTotp, obj)
    {
        PopulatePartialData(cipher);
    }

    public PartialCipherMiniDetailsResponseModel(CipherOrganizationDetailsWithCollections cipher,
        bool orgUseTotp, string obj = "cipherMiniDetails")
        : base(cipher, orgUseTotp, obj)
    {
        PopulatePartialData(cipher);
    }
}

/// <summary>The full-data counterpart of <see cref="CipherMiniDetailsResponseModel"/>.</summary>
public sealed class FullCipherMiniDetailsResponseModel : CipherMiniDetailsResponseModel
{
    public FullCipherMiniDetailsResponseModel(FullCipherAccess access, Cipher cipher,
        GlobalSettings globalSettings,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, bool orgUseTotp,
        string obj = "cipherMiniDetails")
        : base(cipher, collectionCiphers, orgUseTotp, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }

    public FullCipherMiniDetailsResponseModel(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings,
        bool orgUseTotp, string obj = "cipherMiniDetails")
        : base(cipher, orgUseTotp, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }

    public FullCipherMiniDetailsResponseModel(FullCipherAccess access,
        CipherOrganizationDetailsWithCollections cipher, GlobalSettings globalSettings,
        string obj = "cipherMiniDetails")
        : base(cipher, cipher.OrganizationUseTotp, obj)
    {
        PopulateFullData(access, cipher, globalSettings);
    }
}
