namespace Bit.Seeder.Models;

internal record SeedFile
{
    public required List<SeedVaultItem> Items { get; init; }
}

internal record SeedVaultItem
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? Notes { get; init; }
    public SeedLogin? Login { get; init; }
    public SeedCard? Card { get; init; }
    public SeedIdentity? Identity { get; init; }
    public SeedSshKey? SshKey { get; init; }
    public SeedBankAccount? BankAccount { get; init; }
    public SeedDriversLicense? DriversLicense { get; init; }
    public SeedPassport? Passport { get; init; }
    public List<SeedField>? Fields { get; init; }
    public bool? Favorite { get; init; }
    public int? Reprompt { get; init; }
    public string? CipherEncryption { get; init; }
    public List<SeedAttachment>? Attachments { get; init; }
    public bool? Archived { get; init; }
    public bool? Deleted { get; init; }
}

internal record SeedAttachment
{
    public required string File { get; init; }
    public string? FileName { get; init; }
    public required string AttachmentVersion { get; init; }
}

internal record SeedSshKey
{
    public string? PrivateKey { get; init; }
    public string? PublicKey { get; init; }
    public string? KeyFingerprint { get; init; }
}

internal record SeedLogin
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public List<SeedLoginUri>? Uris { get; init; }
    public string? Totp { get; init; }
    public List<SeedFido2Credential>? Fido2Credentials { get; init; }
    public List<SeedPasswordHistory>? PasswordHistory { get; init; }
}

internal record SeedFido2Credential
{
    public string? RpId { get; init; }
    public string? RpName { get; init; }
    public string? UserName { get; init; }
}

internal record SeedPasswordHistory
{
    public required string Password { get; init; }
    public string? LastUsedDate { get; init; }
}

internal record SeedLoginUri
{
    public required string Uri { get; init; }
    public string Match { get; init; } = "domain";
}

internal record SeedCard
{
    public string? CardholderName { get; init; }
    public string? Brand { get; init; }
    public string? Number { get; init; }
    public string? ExpMonth { get; init; }
    public string? ExpYear { get; init; }
    public string? Code { get; init; }
}

internal record SeedIdentity
{
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? Address3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Company { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Ssn { get; init; }
    public string? Username { get; init; }
    public string? PassportNumber { get; init; }
    public string? LicenseNumber { get; init; }
}

internal record SeedBankAccount
{
    public string? BankName { get; init; }
    public string? NameOnAccount { get; init; }
    public string? AccountType { get; init; }
    public string? AccountNumber { get; init; }
    public string? RoutingNumber { get; init; }
    public string? BranchNumber { get; init; }
    public string? Pin { get; init; }
    public string? SwiftCode { get; init; }
    public string? Iban { get; init; }
    public string? BankContactPhone { get; init; }
}

internal record SeedDriversLicense
{
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? DateOfBirth { get; init; }
    public string? LicenseNumber { get; init; }
    public string? IssuingCountry { get; init; }
    public string? IssuingState { get; init; }
    public string? IssueDate { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? ExpirationDate { get; init; }
    public string? LicenseClass { get; init; }
}

internal record SeedPassport
{
    public string? Surname { get; init; }
    public string? GivenName { get; init; }
    public string? DateOfBirth { get; init; }
    public string? Sex { get; init; }
    public string? BirthPlace { get; init; }
    public string? Nationality { get; init; }
    public string? PassportNumber { get; init; }
    public string? PassportType { get; init; }
    public string? IssuingCountry { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? IssueDate { get; init; }
    public string? ExpirationDate { get; init; }
    public string? NationalIdentificationNumber { get; init; }
}

internal record SeedField
{
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string Type { get; init; } = "text";
    public int? LinkedId { get; init; }
}

internal record SeedOrganization
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
}

internal record SeedRoster
{
    public required List<SeedRosterUser> Users { get; init; }
    public List<SeedRosterGroup>? Groups { get; init; }
    public List<SeedRosterCollection>? Collections { get; init; }
}

internal record SeedRosterUser
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Title { get; init; }
    public string Role { get; init; } = "user";
    public string? Branch { get; init; }
    public string? Department { get; init; }
    public List<string>? Folders { get; init; }
}

internal record SeedRosterGroup
{
    public required string Name { get; init; }
    public required List<string> Members { get; init; }
}

internal record SeedRosterCollection
{
    public required string Name { get; init; }
    public List<SeedRosterCollectionGroup>? Groups { get; init; }
    public List<SeedRosterCollectionUser>? Users { get; init; }
}

internal record SeedRosterCollectionGroup
{
    public required string Group { get; init; }
    public bool ReadOnly { get; init; }
    public bool HidePasswords { get; init; }
    public bool Manage { get; init; }
}

internal record SeedRosterCollectionUser
{
    public required string User { get; init; }
    public bool ReadOnly { get; init; }
    public bool HidePasswords { get; init; }
    public bool Manage { get; init; }
}
