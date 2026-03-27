using Bit.Core.Vault.Enums;

namespace Bit.Seeder.Models;

/// <summary>
/// Normalized, strongly-typed plaintext for a single cipher before encryption.
/// Constructed from fixture JSON via FromSeedItem() or programmatically via CipherComposer.
/// </summary>
internal record CipherSeed
{
    /// <summary>
    /// Determines which type-specific DTO (Login, Card, Identity, SecureNote, SshKey)
    /// the factory reads. Exactly one matching DTO must be non-null.
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
    /// Optional plaintext notes (will be encrypted by the factory).
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Optional custom fields (will be encrypted by the factory).
    /// </summary>
    public List<FieldViewDto>? Fields { get; init; }

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
    /// Maps a deserialized <see cref="SeedVaultItem"/> into a <see cref="CipherSeed"/>,
    /// converting Seed* models to their ViewDto counterparts.
    /// EncryptionKey, OrganizationId, and UserId are left null — callers set them via <c>with</c>.
    /// </summary>
    internal static CipherSeed FromSeedItem(SeedVaultItem item) => new()
    {
        Type = MapCipherType(item.Type),
        Name = item.Name,
        Notes = item.Notes,
        Fields = MapFields(item.Fields),
        Login = MapLogin(item.Login),
        Card = MapCard(item.Card),
        Identity = MapIdentity(item.Identity),
        SecureNote = item.Type == "secureNote" ? new SecureNoteViewDto { Type = 0 } : null,
        SshKey = MapSshKey(item.SshKey)
    };

    private static CipherType MapCipherType(string type) => type switch
    {
        "login" => CipherType.Login,
        "card" => CipherType.Card,
        "identity" => CipherType.Identity,
        "secureNote" => CipherType.SecureNote,
        "sshKey" => CipherType.SSHKey,
        _ => throw new ArgumentException($"Unknown cipher type: '{type}'", nameof(type))
    };

    private static List<FieldViewDto>? MapFields(List<SeedField>? fields) =>
        fields?.Select(f => new FieldViewDto
        {
            Name = f.Name,
            Value = f.Value,
            Type = MapFieldType(f.Type)
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
            }).ToList()
        };

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
}
