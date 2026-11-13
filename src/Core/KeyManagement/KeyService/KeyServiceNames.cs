namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Who a key belongs to.
/// </summary>
public enum KeyOwnerType
{
    User,
    Organization,
    Provider,
}

/// <summary>
/// A key's owner.
/// </summary>
public record KeyOwner(KeyOwnerType Type, Guid Id);

/// <summary>
/// The role a key plays for its owner.
/// </summary>
public static class KeyPurposes
{
    public const string UserEncryptionRoot = "user-encryption-root";
    public const string UserSharingRoot = "user-sharing-root";
    public const string UserSigningRoot = "user-signing-root";
    public const string OrganizationSharingRoot = "organization-sharing-root";
}

/// <summary>
/// The resource types of the Key Service API.
/// </summary>
public static class KeyResourceTypes
{
    public const string PublicKey = "public-key";
    public const string PrivateKey = "private-key";
    public const string SigningKey = "signing-key";
    public const string SymmetricKey = "symmetric-key";
    public const string UnlockMethod = "unlock-method";
}

/// <summary>
/// The ways a user reaches their encryption root.
/// </summary>
public static class UnlockMethodKinds
{
    public const string MasterPassword = "master-password";
    public const string KeyConnector = "key-connector";
    public const string Device = "device";
    public const string Passkey = "passkey";
}

/// <summary>
/// Account security versions.
/// </summary>
public static class SecurityVersions
{
    /// <summary>The first version with a signing key.</summary>
    public const int V2 = 2;
}
