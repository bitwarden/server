using System.Globalization;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Enums;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Models;

/// <summary>
/// Normalized, strongly-typed plaintext for a single cipher before encryption.
/// Constructed from fixture JSON via FromSeedItem() or programmatically via CipherComposer.
/// </summary>
internal record CipherSeed
{
    /// <summary>
    /// Drives factory dispatch in <see cref="Steps.CreateCiphersStep"/>. Individual
    /// factories do not read this field — each hard-codes its own type. Exactly one
    /// matching type-specific DTO (Login, Card, Identity, SecureNote, SshKey, BankAccount,
    /// DriversLicense, Passport) must be non-null.
    /// </summary>
    public required CipherType Type { get; init; }

    /// <summary>
    /// Plaintext cipher name (will be encrypted by the factory).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Symmetric key (org key or user key) used for Rust FFI encryption.
    /// </summary>
    public string? EncryptionKey { get; init; }

    /// <summary>
    /// How the cipher's fields are encrypted (user key vs. per-cipher key). Defaults to user key.
    /// </summary>
    public CipherEncryptionType CipherEncryption { get; init; } = CipherEncryptionType.UserKey;

    /// <summary>
    /// Optional plaintext notes (will be encrypted by the factory).
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Optional custom fields (will be encrypted by the factory).
    /// </summary>
    public List<FieldViewDto>? Fields { get; init; }

    /// <summary>
    /// Master-password re-prompt (0 = None, 1 = Password).
    /// </summary>
    public CipherRepromptType Reprompt { get; init; }

    /// <summary>
    /// Owning organization. Null for personal vault ciphers.
    /// </summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>
    /// Owning user for personal vault ciphers. Null for organization ciphers.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Plaintext login data (username, password, URIs). Non-null when Type is Login.
    /// </summary>
    public LoginViewDto? Login { get; init; }

    /// <summary>
    /// Plaintext card data (cardholder, number, expiry). Non-null when Type is Card.
    /// </summary>
    public CardViewDto? Card { get; init; }

    /// <summary>
    /// Plaintext identity data (name, address, documents). Non-null when Type is Identity.
    /// </summary>
    public IdentityViewDto? Identity { get; init; }

    /// <summary>
    /// Secure note type marker. Non-null when Type is SecureNote.
    /// The actual note content is carried by the Notes property, not this DTO.
    /// </summary>
    public SecureNoteViewDto? SecureNote { get; init; }

    /// <summary>
    /// Plaintext SSH key data (private key, public key, fingerprint). Non-null when Type is SSHKey.
    /// </summary>
    public SshKeyViewDto? SshKey { get; init; }

    /// <summary>
    /// Plaintext bank account data. Non-null when Type is BankAccount.
    /// </summary>
    public BankAccountViewDto? BankAccount { get; init; }

    /// <summary>
    /// Plaintext driver's license data. Non-null when Type is DriversLicense.
    /// </summary>
    public DriversLicenseViewDto? DriversLicense { get; init; }

    /// <summary>
    /// Plaintext passport data. Non-null when Type is Passport.
    /// </summary>
    public PassportViewDto? Passport { get; init; }

    /// <summary>
    /// Validates that required fields are set before factory consumption.
    /// Call after populating EncryptionKey via <c>with</c>.
    /// </summary>
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(EncryptionKey);
    }

    /// <summary>
    /// Maps a deserialized <see cref="SeedVaultItem"/> into a <see cref="CipherSeed"/>,
    /// converting Seed* models to their ViewDto counterparts.
    /// EncryptionKey, OrganizationId, and UserId are left null — callers set them via <c>with</c>.
    /// </summary>
    internal static CipherSeed FromSeedItem(SeedVaultItem item) => new()
    {
        Type = MapCipherType(item.Type),
        Name = item.Name,
        Notes = item.Notes,
        CipherEncryption = ParseCipherEncryption(item.CipherEncryption),
        Reprompt = item.Reprompt == 1 ? CipherRepromptType.Password : CipherRepromptType.None,
        Fields = MapFields(item.Fields),
        Login = MapLogin(item.Login),
        Card = MapCard(item.Card),
        Identity = MapIdentity(item.Identity),
        SecureNote = item.Type == "secureNote" ? new SecureNoteViewDto { Type = 0 } : null,
        SshKey = MapSshKey(item.SshKey),
        BankAccount = MapBankAccount(item.BankAccount),
        DriversLicense = MapDriversLicense(item.DriversLicense),
        Passport = MapPassport(item.Passport)
    };

    private static CipherType MapCipherType(string type) => type switch
    {
        "login" => CipherType.Login,
        "card" => CipherType.Card,
        "identity" => CipherType.Identity,
        "secureNote" => CipherType.SecureNote,
        "sshKey" => CipherType.SSHKey,
        "bankAccount" => CipherType.BankAccount,
        "driversLicense" => CipherType.DriversLicense,
        "passport" => CipherType.Passport,
        _ => throw new ArgumentException($"Unknown cipher type: '{type}'", nameof(type))
    };

    private static CipherEncryptionType ParseCipherEncryption(string? value) => value switch
    {
        null or "userKey" => CipherEncryptionType.UserKey,
        "cipherKey" => CipherEncryptionType.CipherKey,
        _ => throw new ArgumentException($"Unknown cipherEncryption: '{value}'. Expected \"userKey\" or \"cipherKey\".", nameof(value))
    };

    private static List<FieldViewDto>? MapFields(List<SeedField>? fields) =>
        fields?.Select(f => new FieldViewDto
        {
            Name = f.Name,
            Value = f.Value,
            Type = MapFieldType(f.Type),
            LinkedId = f.LinkedId
        }).ToList();

    private static int MapFieldType(string type) => type switch
    {
        "hidden" => 1,
        "boolean" => 2,
        "linked" => 3,
        "text" => 0,
        _ => throw new ArgumentException($"Unknown field type: '{type}'", nameof(type))
    };

    private static LoginViewDto? MapLogin(SeedLogin? login) =>
        login == null ? null : new LoginViewDto
        {
            Username = login.Username,
            Password = login.Password,
            Totp = login.Totp,
            Uris = login.Uris?.Select(u => new LoginUriViewDto
            {
                Uri = u.Uri,
                Match = MapUriMatchType(u.Match)
            }).ToList(),
            // Key material is synthesized; the fixture only supplies the relying-party/user identifiers.
            Fido2Credentials = login.Fido2Credentials?.Select(f => LoginCipherSeeder.CreateFido2Credential(
                f.RpId ?? "example.com",
                f.RpName ?? f.RpId ?? "Example",
                f.UserName ?? login.Username ?? "user")).ToList(),
            PasswordHistory = MapPasswordHistory(login.PasswordHistory)
        };

    private static List<PasswordHistoryViewDto>? MapPasswordHistory(List<SeedPasswordHistory>? history) =>
        history?.Select(p =>
        {
            var dto = new PasswordHistoryViewDto { Password = p.Password };
            if (p.LastUsedDate is not null)
            {
                dto.LastUsedDate = DateTime.Parse(p.LastUsedDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            return dto;
        }).ToList();

    private static int MapUriMatchType(string match) => match switch
    {
        "host" => 1,
        "startsWith" => 2,
        "exact" => 3,
        "regex" => 4,
        "never" => 5,
        "domain" => 0,
        _ => throw new ArgumentException($"Unknown URI match type: '{match}'", nameof(match))
    };

    private static CardViewDto? MapCard(SeedCard? card) =>
        card == null ? null : new CardViewDto
        {
            CardholderName = card.CardholderName,
            Brand = card.Brand,
            Number = card.Number,
            ExpMonth = card.ExpMonth,
            ExpYear = card.ExpYear,
            Code = card.Code
        };

    private static IdentityViewDto? MapIdentity(SeedIdentity? identity) =>
        identity == null ? null : new IdentityViewDto
        {
            FirstName = identity.FirstName,
            MiddleName = identity.MiddleName,
            LastName = identity.LastName,
            Address1 = identity.Address1,
            Address2 = identity.Address2,
            Address3 = identity.Address3,
            City = identity.City,
            State = identity.State,
            PostalCode = identity.PostalCode,
            Country = identity.Country,
            Company = identity.Company,
            Email = identity.Email,
            Phone = identity.Phone,
            SSN = identity.Ssn,
            Username = identity.Username,
            PassportNumber = identity.PassportNumber,
            LicenseNumber = identity.LicenseNumber
        };

    private static SshKeyViewDto? MapSshKey(SeedSshKey? sshKey) =>
        sshKey == null ? null : new SshKeyViewDto
        {
            PrivateKey = sshKey.PrivateKey,
            PublicKey = sshKey.PublicKey,
            Fingerprint = sshKey.KeyFingerprint
        };

    private static BankAccountViewDto? MapBankAccount(SeedBankAccount? bankAccount) =>
        bankAccount == null ? null : new BankAccountViewDto
        {
            BankName = bankAccount.BankName,
            NameOnAccount = bankAccount.NameOnAccount,
            AccountType = bankAccount.AccountType,
            AccountNumber = bankAccount.AccountNumber,
            RoutingNumber = bankAccount.RoutingNumber,
            BranchNumber = bankAccount.BranchNumber,
            Pin = bankAccount.Pin,
            SwiftCode = bankAccount.SwiftCode,
            Iban = bankAccount.Iban,
            BankContactPhone = bankAccount.BankContactPhone
        };

    private static DriversLicenseViewDto? MapDriversLicense(SeedDriversLicense? driversLicense) =>
        driversLicense == null ? null : new DriversLicenseViewDto
        {
            FirstName = driversLicense.FirstName,
            MiddleName = driversLicense.MiddleName,
            LastName = driversLicense.LastName,
            DateOfBirth = driversLicense.DateOfBirth,
            LicenseNumber = driversLicense.LicenseNumber,
            IssuingCountry = driversLicense.IssuingCountry,
            IssuingState = driversLicense.IssuingState,
            IssueDate = driversLicense.IssueDate,
            IssuingAuthority = driversLicense.IssuingAuthority,
            ExpirationDate = driversLicense.ExpirationDate,
            LicenseClass = driversLicense.LicenseClass
        };

    private static PassportViewDto? MapPassport(SeedPassport? passport) =>
        passport == null ? null : new PassportViewDto
        {
            Surname = passport.Surname,
            GivenName = passport.GivenName,
            DateOfBirth = passport.DateOfBirth,
            Sex = passport.Sex,
            BirthPlace = passport.BirthPlace,
            Nationality = passport.Nationality,
            PassportNumber = passport.PassportNumber,
            PassportType = passport.PassportType,
            IssuingCountry = passport.IssuingCountry,
            IssuingAuthority = passport.IssuingAuthority,
            IssueDate = passport.IssueDate,
            ExpirationDate = passport.ExpirationDate,
            NationalIdentificationNumber = passport.NationalIdentificationNumber
        };
}
