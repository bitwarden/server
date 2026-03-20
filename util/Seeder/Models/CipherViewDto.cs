using System.Text.Json.Serialization;
using Bit.Seeder.Attributes;

namespace Bit.Seeder.Models;

public class CipherViewDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid? OrganizationId { get; set; }

    [JsonPropertyName("folderId")]
    public Guid? FolderId { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [EncryptProperty]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [EncryptProperty]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("login")]
    public LoginViewDto? Login { get; set; }

    [JsonPropertyName("identity")]
    public IdentityViewDto? Identity { get; set; }

    [JsonPropertyName("card")]
    public CardViewDto? Card { get; set; }

    [JsonPropertyName("secureNote")]
    public SecureNoteViewDto? SecureNote { get; set; }

    [JsonPropertyName("sshKey")]
    public SshKeyViewDto? SshKey { get; set; }

    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }

    [JsonPropertyName("reprompt")]
    public int Reprompt { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldViewDto>? Fields { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedDate")]
    public DateTime? DeletedDate { get; set; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
}

public class LoginViewDto
{
    [EncryptProperty]
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [EncryptProperty]
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("passwordRevisionDate")]
    public DateTime? PasswordRevisionDate { get; set; }

    [JsonPropertyName("uris")]
    public List<LoginUriViewDto>? Uris { get; set; }

    [EncryptProperty]
    [JsonPropertyName("totp")]
    public string? Totp { get; set; }
}

public class LoginUriViewDto
{
    [EncryptProperty]
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("match")]
    public int? Match { get; set; }

    [EncryptProperty]
    [JsonPropertyName("uriChecksum")]
    public string? UriChecksum { get; set; }
}

public class FieldViewDto
{
    [EncryptProperty]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [EncryptProperty]
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("linkedId")]
    public int? LinkedId { get; set; }
}

public static class CipherTypes
{
    public const int Login = 1;
    public const int SecureNote = 2;
    public const int Card = 3;
    public const int Identity = 4;
    public const int SshKey = 5;
}

public static class RepromptTypes
{
    public const int None = 0;
    public const int Password = 1;
}

/// <summary>
/// Card cipher data. Uses record for composition via `with` expressions.
/// </summary>
public record CardViewDto
{
    [EncryptProperty]
    [JsonPropertyName("cardholderName")]
    public string? CardholderName { get; init; }

    [EncryptProperty]
    [JsonPropertyName("brand")]
    public string? Brand { get; init; }

    [EncryptProperty]
    [JsonPropertyName("number")]
    public string? Number { get; init; }

    [EncryptProperty]
    [JsonPropertyName("expMonth")]
    public string? ExpMonth { get; init; }

    [EncryptProperty]
    [JsonPropertyName("expYear")]
    public string? ExpYear { get; init; }

    [EncryptProperty]
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

/// <summary>
/// Identity cipher data. Uses record for composition via `with` expressions.
/// </summary>
public record IdentityViewDto
{
    [EncryptProperty]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [EncryptProperty]
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [EncryptProperty]
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; init; }

    [EncryptProperty]
    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [EncryptProperty]
    [JsonPropertyName("address1")]
    public string? Address1 { get; init; }

    [EncryptProperty]
    [JsonPropertyName("address2")]
    public string? Address2 { get; init; }

    [EncryptProperty]
    [JsonPropertyName("address3")]
    public string? Address3 { get; init; }

    [EncryptProperty]
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [EncryptProperty]
    [JsonPropertyName("state")]
    public string? State { get; init; }

    [EncryptProperty]
    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [EncryptProperty]
    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [EncryptProperty]
    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [EncryptProperty]
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [EncryptProperty]
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [EncryptProperty]
    [JsonPropertyName("ssn")]
    public string? SSN { get; init; }

    [EncryptProperty]
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [EncryptProperty]
    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; init; }

    [EncryptProperty]
    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; init; }
}

/// <summary>
/// SecureNote cipher data. Minimal structure - content is in cipher.Notes.
/// </summary>
public record SecureNoteViewDto
{
    [JsonPropertyName("type")]
    public int Type { get; init; } = 0; // Generic = 0
}

/// <summary>
/// SSH Key cipher data. Uses record for composition via `with` expressions.
/// </summary>
public record SshKeyViewDto
{
    [EncryptProperty]
    [JsonPropertyName("privateKey")]
    public string? PrivateKey { get; init; }

    [EncryptProperty]
    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; init; }

    [EncryptProperty]
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }
}
