using System.Text.Json.Serialization;

namespace Bit.Seeder.Models;

public class CipherViewDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid? OrganizationId { get; set; }

    [JsonPropertyName("folderId")]
    public Guid? FolderId { get; set; }

    [JsonPropertyName("collectionIds")]
    public List<Guid> CollectionIds { get; set; } = [];

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

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

    [JsonPropertyName("bankAccount")]
    public BankAccountViewDto? BankAccount { get; set; }

    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }

    [JsonPropertyName("reprompt")]
    public int Reprompt { get; set; }

    [JsonPropertyName("organizationUseTotp")]
    public bool OrganizationUseTotp { get; set; }

    [JsonPropertyName("edit")]
    public bool Edit { get; set; } = true;

    [JsonPropertyName("permissions")]
    public object? Permissions { get; set; }

    [JsonPropertyName("viewPassword")]
    public bool ViewPassword { get; set; } = true;

    [JsonPropertyName("localData")]
    public object? LocalData { get; set; }

    [JsonPropertyName("attachments")]
    public object? Attachments { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldViewDto>? Fields { get; set; }

    [JsonPropertyName("passwordHistory")]
    public object? PasswordHistory { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedDate")]
    public DateTime? DeletedDate { get; set; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("archivedDate")]
    public DateTime? ArchivedDate { get; set; }
}

public class LoginViewDto
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("passwordRevisionDate")]
    public DateTime? PasswordRevisionDate { get; set; }

    [JsonPropertyName("uris")]
    public List<LoginUriViewDto>? Uris { get; set; }

    [JsonPropertyName("totp")]
    public string? Totp { get; set; }

    [JsonPropertyName("autofillOnPageLoad")]
    public bool? AutofillOnPageLoad { get; set; }

    [JsonPropertyName("fido2Credentials")]
    public object? Fido2Credentials { get; set; }
}

public class LoginUriViewDto
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("match")]
    public int? Match { get; set; }

    [JsonPropertyName("uriChecksum")]
    public string? UriChecksum { get; set; }
}

public class FieldViewDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

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
    public const int BankAccount = 6;
}

public static class RepromptTypes
{
    public const int None = 0;
    public const int Password = 1;
}

/// <summary>
/// Card cipher data for SDK encryption. Uses record for composition via `with` expressions.
/// </summary>
public record CardViewDto
{
    [JsonPropertyName("cardholderName")]
    public string? CardholderName { get; init; }

    [JsonPropertyName("brand")]
    public string? Brand { get; init; }

    [JsonPropertyName("number")]
    public string? Number { get; init; }

    [JsonPropertyName("expMonth")]
    public string? ExpMonth { get; init; }

    [JsonPropertyName("expYear")]
    public string? ExpYear { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

/// <summary>
/// Identity cipher data for SDK encryption. Uses record for composition via `with` expressions.
/// </summary>
public record IdentityViewDto
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("address1")]
    public string? Address1 { get; init; }

    [JsonPropertyName("address2")]
    public string? Address2 { get; init; }

    [JsonPropertyName("address3")]
    public string? Address3 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("ssn")]
    public string? SSN { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; init; }

    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; init; }
}

/// <summary>
/// SecureNote cipher data for SDK encryption. Minimal structure - content is in cipher.Notes.
/// </summary>
public record SecureNoteViewDto
{
    [JsonPropertyName("type")]
    public int Type { get; init; } = 0; // Generic = 0
}

/// <summary>
/// SSH Key cipher data for SDK encryption. Uses record for composition via `with` expressions.
/// </summary>
public record SshKeyViewDto
{
    [JsonPropertyName("privateKey")]
    public string? PrivateKey { get; init; }

    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; init; }

    /// <summary>SDK expects "fingerprint" field name.</summary>
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }
}

/// <summary>
/// Bank Account cipher data for SDK encryption. Uses record for composition via `with` expressions.
/// </summary>
public record BankAccountViewDto
{
    [JsonPropertyName("bankName")]
    public string? BankName { get; init; }

    [JsonPropertyName("nameOnAccount")]
    public string? NameOnAccount { get; init; }

    [JsonPropertyName("accountType")]
    public string? AccountType { get; init; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("routingNumber")]
    public string? RoutingNumber { get; init; }

    [JsonPropertyName("branchNumber")]
    public string? BranchNumber { get; init; }

    [JsonPropertyName("pin")]
    public string? Pin { get; init; }

    [JsonPropertyName("swiftCode")]
    public string? SwiftCode { get; init; }

    [JsonPropertyName("iban")]
    public string? Iban { get; init; }

    [JsonPropertyName("bankContactPhone")]
    public string? BankContactPhone { get; init; }
}
