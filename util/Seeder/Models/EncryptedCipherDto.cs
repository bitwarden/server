using System.Text.Json.Serialization;

namespace Bit.Seeder.Models;

public class EncryptedCipherDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid? OrganizationId { get; set; }

    [JsonPropertyName("folderId")]
    public Guid? FolderId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("login")]
    public EncryptedLoginDto? Login { get; set; }

    [JsonPropertyName("card")]
    public EncryptedCardDto? Card { get; set; }

    [JsonPropertyName("identity")]
    public EncryptedIdentityDto? Identity { get; set; }

    [JsonPropertyName("secureNote")]
    public EncryptedSecureNoteDto? SecureNote { get; set; }

    [JsonPropertyName("sshKey")]
    public EncryptedSshKeyDto? SshKey { get; set; }

    // TODO: Remove JsonIgnore condition when Rust SDK supports BankAccount cipher type
    [JsonPropertyName("bankAccount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EncryptedBankAccountDto? BankAccount { get; set; }

    // TODO: Remove JsonIgnore condition when Rust SDK supports DriversLicense cipher type
    [JsonPropertyName("driversLicense")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EncryptedDriversLicenseDto? DriversLicense { get; set; }

    // TODO: Remove JsonIgnore condition when Rust SDK supports Passport cipher type
    [JsonPropertyName("passport")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EncryptedPassportDto? Passport { get; set; }

    [JsonPropertyName("fields")]
    public List<EncryptedFieldDto>? Fields { get; set; }

    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }

    [JsonPropertyName("reprompt")]
    public int Reprompt { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; set; }

    [JsonPropertyName("deletedDate")]
    public DateTime? DeletedDate { get; set; }
}

public class EncryptedLoginDto
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("totp")]
    public string? Totp { get; set; }

    [JsonPropertyName("uris")]
    public List<EncryptedLoginUriDto>? Uris { get; set; }

    [JsonPropertyName("passwordRevisionDate")]
    public DateTime? PasswordRevisionDate { get; set; }

    [JsonPropertyName("fido2Credentials")]
    public List<EncryptedFido2CredentialDto>? Fido2Credentials { get; set; }
}

public class EncryptedLoginUriDto
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("match")]
    public int? Match { get; set; }

    [JsonPropertyName("uriChecksum")]
    public string? UriChecksum { get; set; }
}

public class EncryptedFido2CredentialDto
{
    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }
    [JsonPropertyName("discoverable")]
    public required string Discoverable { get; set; }
    [JsonPropertyName("credentialId")]
    public required string CredentialId { get; set; }
    [JsonPropertyName("keyType")]
    public required string KeyType { get; set; }
    [JsonPropertyName("keyAlgorithm")]
    public required string KeyAlgorithm { get; set; }
    [JsonPropertyName("keyCurve")]
    public required string KeyCurve { get; set; }
    [JsonPropertyName("keyValue")]
    public required string KeyValue { get; set; }
    [JsonPropertyName("counter")]
    public required string Counter { get; set; }
    [JsonPropertyName("rpId")]
    public required string RpId { get; set; }
    [JsonPropertyName("rpName")]
    public required string RpName { get; set; }
    [JsonPropertyName("userHandle")]
    public required string UserHandle { get; set; }
    [JsonPropertyName("userName")]
    public required string UserName { get; set; }
    [JsonPropertyName("userDisplayName")]
    public required string UserDisplayName { get; set; }
}

public class EncryptedFieldDto
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

public class EncryptedCardDto
{
    [JsonPropertyName("cardholderName")]
    public string? CardholderName { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("expMonth")]
    public string? ExpMonth { get; set; }

    [JsonPropertyName("expYear")]
    public string? ExpYear { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class EncryptedIdentityDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("address1")]
    public string? Address1 { get; set; }

    [JsonPropertyName("address2")]
    public string? Address2 { get; set; }

    [JsonPropertyName("address3")]
    public string? Address3 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("ssn")]
    public string? SSN { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; set; }

    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; set; }
}

public class EncryptedSecureNoteDto
{
    [JsonPropertyName("type")]
    public int Type { get; set; }
}

public class EncryptedSshKeyDto
{
    [JsonPropertyName("privateKey")]
    public string? PrivateKey { get; set; }

    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }
}

public class EncryptedBankAccountDto
{
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    [JsonPropertyName("nameOnAccount")]
    public string? NameOnAccount { get; set; }

    [JsonPropertyName("accountType")]
    public string? AccountType { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("routingNumber")]
    public string? RoutingNumber { get; set; }

    [JsonPropertyName("branchNumber")]
    public string? BranchNumber { get; set; }

    [JsonPropertyName("pin")]
    public string? Pin { get; set; }

    [JsonPropertyName("swiftCode")]
    public string? SwiftCode { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [JsonPropertyName("bankContactPhone")]
    public string? BankContactPhone { get; set; }
}

public class EncryptedDriversLicenseDto
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; set; }

    [JsonPropertyName("issuingCountry")]
    public string? IssuingCountry { get; set; }

    [JsonPropertyName("issuingState")]
    public string? IssuingState { get; set; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("issuingAuthority")]
    public string? IssuingAuthority { get; set; }

    [JsonPropertyName("expirationDate")]
    public string? ExpirationDate { get; set; }

    [JsonPropertyName("licenseClass")]
    public string? LicenseClass { get; set; }
}

public class EncryptedPassportDto
{
    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("sex")]
    public string? Sex { get; set; }

    [JsonPropertyName("birthPlace")]
    public string? BirthPlace { get; set; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; set; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; set; }

    [JsonPropertyName("passportType")]
    public string? PassportType { get; set; }

    [JsonPropertyName("issuingCountry")]
    public string? IssuingCountry { get; set; }

    [JsonPropertyName("issuingAuthority")]
    public string? IssuingAuthority { get; set; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("expirationDate")]
    public string? ExpirationDate { get; set; }

    [JsonPropertyName("nationalIdentificationNumber")]
    public string? NationalIdentificationNumber { get; set; }
}
