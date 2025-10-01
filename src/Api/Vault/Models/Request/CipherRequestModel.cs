// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Request;

public class CipherRequestModel
{
    /// <summary>
    /// The Id of the user that encrypted the cipher. It should always represent a UserId.
    /// </summary>
    public Guid? EncryptedFor { get; set; }
    public CipherType Type { get; set; }

    [StringLength(36)]
    public string OrganizationId { get; set; }
    public string FolderId { get; set; }
    public bool Favorite { get; set; }
    public CipherRepromptType Reprompt { get; set; }
    public string Key { get; set; }
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }
    [EncryptedString]
    [EncryptedStringLength(10000)]
    public string Notes { get; set; }
    public IEnumerable<CipherFieldModel> Fields { get; set; }
    public IEnumerable<CipherPasswordHistoryModel> PasswordHistory { get; set; }
    [Obsolete]
    public Dictionary<string, string> Attachments { get; set; }
    // TODO: Rename to Attachments whenever the above is finally removed.
    public Dictionary<string, CipherAttachmentModel> Attachments2 { get; set; }

    [Obsolete("Use Data instead.")]
    public CipherLoginModel Login { get; set; }

    [Obsolete("Use Data instead.")]
    public CipherCardModel Card { get; set; }

    [Obsolete("Use Data instead.")]
    public CipherIdentityModel Identity { get; set; }

    [Obsolete("Use Data instead.")]
    public CipherSecureNoteModel SecureNote { get; set; }

    [Obsolete("Use Data instead.")]
    public CipherSSHKeyModel SSHKey { get; set; }

    /// <summary>
    /// JSON string containing cipher-specific data
    /// </summary>
    [StringLength(500000)]
    public string Data { get; set; }
    public DateTime? LastKnownRevisionDate { get; set; } = null;
    public DateTime? ArchivedDate { get; set; }

    public CipherDetails ToCipherDetails(Guid userId, bool allowOrgIdSet = true)
    {
        var hasOrgId = !string.IsNullOrWhiteSpace(OrganizationId);
        var cipher = new CipherDetails
        {
            Type = Type,
            UserId = !hasOrgId ? (Guid?)userId : null,
            OrganizationId = allowOrgIdSet && hasOrgId ? new Guid(OrganizationId) : (Guid?)null,
            Edit = true,
            ViewPassword = true,
        };
        ToCipherDetails(cipher);
        return cipher;
    }

    public CipherDetails ToCipherDetails(CipherDetails existingCipher)
    {
        existingCipher.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId);
        existingCipher.Favorite = Favorite;
        ToCipher(existingCipher);
        return existingCipher;
    }

    public Cipher ToCipher(Cipher existingCipher, Guid? userId = null)
    {
        // If Data field is provided, use it directly
        if (!string.IsNullOrWhiteSpace(Data))
        {
            existingCipher.Data = Data;
        }
        else
        {
            // Fallback to structured fields
            switch (existingCipher.Type)
            {
                case CipherType.Login:
                    var loginData = ToCipherLoginData();
                    var loginJson = JsonSerializer.Serialize(loginData, JsonHelpers.IgnoreWritingNull);
                    var loginObj = JsonDocument.Parse(loginJson);
                    var loginDict = JsonSerializer.Deserialize<Dictionary<string, object>>(loginJson);
                    loginDict?.Remove(nameof(CipherLoginData.Uri));

                    existingCipher.Data = JsonSerializer.Serialize(loginDict, JsonHelpers.IgnoreWritingNull);
                    break;
                case CipherType.Card:
                    existingCipher.Data = JsonSerializer.Serialize(ToCipherCardData(), JsonHelpers.IgnoreWritingNull);
                    break;
                case CipherType.Identity:
                    existingCipher.Data =
                        JsonSerializer.Serialize(ToCipherIdentityData(), JsonHelpers.IgnoreWritingNull);
                    break;
                case CipherType.SecureNote:
                    existingCipher.Data =
                        JsonSerializer.Serialize(ToCipherSecureNoteData(), JsonHelpers.IgnoreWritingNull);
                    break;
                case CipherType.SSHKey:
                    existingCipher.Data = JsonSerializer.Serialize(ToCipherSSHKeyData(), JsonHelpers.IgnoreWritingNull);
                    break;
                default:
                    throw new ArgumentException("Unsupported type: " + nameof(Type) + ".");
            }
        }

        var userIdKey = userId.HasValue ? userId.ToString().ToUpperInvariant() : null;
        existingCipher.Reprompt = Reprompt;
        existingCipher.Key = Key;
        existingCipher.ArchivedDate = ArchivedDate;
        existingCipher.Folders = UpdateUserSpecificJsonField(existingCipher.Folders, userIdKey, FolderId);
        existingCipher.Favorites = UpdateUserSpecificJsonField(existingCipher.Favorites, userIdKey, Favorite);

        var hasAttachments2 = (Attachments2?.Count ?? 0) > 0;
        var hasAttachments = (Attachments?.Count ?? 0) > 0;

        if (!hasAttachments2 && !hasAttachments)
        {
            return existingCipher;
        }

        var attachments = existingCipher.GetAttachments();
        if ((attachments?.Count ?? 0) == 0)
        {
            return existingCipher;
        }

        if (hasAttachments2)
        {
            foreach (var attachment in attachments)
            {
                if (!Attachments2.TryGetValue(attachment.Key, out var attachment2))
                {
                    continue;
                }
                attachment.Value.FileName = attachment2.FileName;
                attachment.Value.Key = attachment2.Key;
            }
        }
        else if (hasAttachments)
        {
            foreach (var attachment in attachments)
            {
                if (!Attachments.TryGetValue(attachment.Key, out var attachmentForKey))
                {
                    continue;
                }
                attachment.Value.FileName = attachmentForKey;
                attachment.Value.Key = null;
            }
        }

        existingCipher.SetAttachments(attachments);
        return existingCipher;
    }

    public Cipher ToOrganizationCipher()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentNullException(nameof(OrganizationId));
        }

        return ToCipher(new Cipher
        {
            Type = Type,
            OrganizationId = new Guid(OrganizationId)
        });
    }

    public CipherDetails ToOrganizationCipherDetails(Guid orgId)
    {
        return ToCipherDetails(new CipherDetails
        {
            Type = Type,
            OrganizationId = orgId,
            Edit = true
        });
    }

    private CipherLoginData ToCipherLoginData()
    {
        return new CipherLoginData
        {
            Name = Name,
            Notes = Notes,
            Fields = Fields?.Select(f => f.ToCipherFieldData()),
            PasswordHistory = PasswordHistory?.Select(ph => ph.ToCipherPasswordHistoryData()),

            Uris =
                Login.Uris?.Where(u => u != null)
                    .Select(u => u.ToCipherLoginUriData()),
            Username = Login.Username,
            Password = Login.Password,
            PasswordRevisionDate = Login.PasswordRevisionDate,
            Totp = Login.Totp,
            AutofillOnPageLoad = Login.AutofillOnPageLoad,
            Fido2Credentials = Login.Fido2Credentials == null ? null : Login.Fido2Credentials.ToCipherLoginFido2CredentialData(),
        };
    }

    private CipherIdentityData ToCipherIdentityData()
    {
        return new CipherIdentityData
        {
            Name = Name,
            Notes = Notes,
            Fields = Fields?.Select(f => f.ToCipherFieldData()),
            PasswordHistory = PasswordHistory?.Select(ph => ph.ToCipherPasswordHistoryData()),

            Title = Identity.Title,
            FirstName = Identity.FirstName,
            MiddleName = Identity.MiddleName,
            LastName = Identity.LastName,
            Address1 = Identity.Address1,
            Address2 = Identity.Address2,
            Address3 = Identity.Address3,
            City = Identity.City,
            State = Identity.State,
            PostalCode = Identity.PostalCode,
            Country = Identity.Country,
            Company = Identity.Company,
            Email = Identity.Email,
            Phone = Identity.Phone,
            SSN = Identity.SSN,
            Username = Identity.Username,
            PassportNumber = Identity.PassportNumber,
            LicenseNumber = Identity.LicenseNumber,
        };
    }

    private CipherCardData ToCipherCardData()
    {
        return new CipherCardData
        {
            Name = Name,
            Notes = Notes,
            Fields = Fields?.Select(f => f.ToCipherFieldData()),
            PasswordHistory = PasswordHistory?.Select(ph => ph.ToCipherPasswordHistoryData()),

            CardholderName = Card.CardholderName,
            Brand = Card.Brand,
            Number = Card.Number,
            ExpMonth = Card.ExpMonth,
            ExpYear = Card.ExpYear,
            Code = Card.Code,
        };
    }

    private CipherSecureNoteData ToCipherSecureNoteData()
    {
        return new CipherSecureNoteData
        {
            Name = Name,
            Notes = Notes,
            Fields = Fields?.Select(f => f.ToCipherFieldData()),
            PasswordHistory = PasswordHistory?.Select(ph => ph.ToCipherPasswordHistoryData()),

            Type = SecureNote.Type,
        };
    }

    private CipherSSHKeyData ToCipherSSHKeyData()
    {
        return new CipherSSHKeyData
        {
            Name = Name,
            Notes = Notes,
            Fields = Fields?.Select(f => f.ToCipherFieldData()),
            PasswordHistory = PasswordHistory?.Select(ph => ph.ToCipherPasswordHistoryData()),

            PrivateKey = SSHKey.PrivateKey,
            PublicKey = SSHKey.PublicKey,
            KeyFingerprint = SSHKey.KeyFingerprint,
        };
    }

    /// <summary>
    /// Updates a JSON string representing a dictionary by adding, updating, or removing a key-value pair
    /// based on the provided userIdKey and newValue.
    /// </summary>
    private static string UpdateUserSpecificJsonField(string existingJson, string userIdKey, object newValue)
    {
        if (userIdKey == null)
        {
            return existingJson;
        }

        var jsonDict = string.IsNullOrWhiteSpace(existingJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new Dictionary<string, object>();

        // Remove the key from the value when:
        // - new value is null
        // - new value is an empty or whitespace string
        // - new value is a boolean false
        var shouldRemove = newValue == null ||
                          (newValue is string strValue && string.IsNullOrWhiteSpace(strValue)) ||
                          (newValue is bool boolValue && !boolValue);

        if (shouldRemove)
        {
            jsonDict.Remove(userIdKey);
        }
        else
        {
            jsonDict[userIdKey] = newValue is string str ? str.ToUpperInvariant() : newValue;
        }

        return jsonDict.Count == 0 ? null : JsonSerializer.Serialize(jsonDict);
    }
}

public class CipherWithIdRequestModel : CipherRequestModel
{
    [Required]
    public Guid? Id { get; set; }
}

public class CipherCreateRequestModel : IValidatableObject
{
    public IEnumerable<Guid> CollectionIds { get; set; }
    [Required]
    public CipherRequestModel Cipher { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Cipher.OrganizationId) && (!CollectionIds?.Any() ?? true))
        {
            yield return new ValidationResult("You must select at least one collection.",
               new string[] { nameof(CollectionIds) });
        }
    }
}

public class CipherShareRequestModel : IValidatableObject
{
    [Required]
    public IEnumerable<string> CollectionIds { get; set; }
    [Required]
    public CipherRequestModel Cipher { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Cipher.OrganizationId))
        {
            yield return new ValidationResult("Cipher OrganizationId is required.",
                new string[] { nameof(Cipher.OrganizationId) });
        }

        if (!CollectionIds?.Any() ?? true)
        {
            yield return new ValidationResult("You must select at least one collection.",
                new string[] { nameof(CollectionIds) });
        }
    }
}

public class CipherCollectionsRequestModel
{
    [Required]
    public IEnumerable<string> CollectionIds { get; set; }
}

public class CipherBulkArchiveRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}

public class CipherBulkDeleteRequestModel
{
    [Required]
    public IEnumerable<string> Ids { get; set; }
    public string OrganizationId { get; set; }
}

public class CipherBulkUnarchiveRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}

public class CipherBulkRestoreRequestModel
{
    [Required]
    public IEnumerable<string> Ids { get; set; }
    public Guid OrganizationId { get; set; }
}

public class CipherBulkMoveRequestModel
{
    [Required]
    public IEnumerable<string> Ids { get; set; }
    public string FolderId { get; set; }
}

public class CipherBulkShareRequestModel : IValidatableObject
{
    [Required]
    public IEnumerable<string> CollectionIds { get; set; }
    [Required]
    public IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Ciphers?.Any() ?? true)
        {
            yield return new ValidationResult("You must select at least one cipher.",
                new string[] { nameof(Ciphers) });
        }
        else
        {
            var allHaveIds = true;
            var organizationIds = new HashSet<string>();
            foreach (var c in Ciphers)
            {
                organizationIds.Add(c.OrganizationId);
                if (allHaveIds)
                {
                    allHaveIds = !(!c.Id.HasValue || string.IsNullOrWhiteSpace(c.OrganizationId));
                }
            }

            if (!allHaveIds)
            {
                yield return new ValidationResult("All Ciphers must have an Id and OrganizationId.",
                    new string[] { nameof(Ciphers) });
            }
            else if (organizationIds.Count != 1)
            {
                yield return new ValidationResult("All ciphers must be for the same organization.");
            }
        }

        if (!CollectionIds?.Any() ?? true)
        {
            yield return new ValidationResult("You must select at least one collection.",
                new string[] { nameof(CollectionIds) });
        }
    }
}
