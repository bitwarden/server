using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Builds the <c>data</c> JSON string of each Key Service resource from the stored key columns.
/// </summary>
public static class KeyData
{
    private const string Pbkdf2 = "pbkdf2";
    private const string Argon2id = "argon2id";
    private const string VerifyingKeyType = "verifying-key";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// A public key: <c>{"type":"public-key","publicKey":"MIIB...","signedKey":"hFgd..."}</c>.
    /// </summary>
    public static string PublicKey(string publicKey, string? signedKey) =>
        Serialize(new { type = KeyResourceTypes.PublicKey, publicKey, signedKey });

    /// <summary>
    /// A verifying key: <c>{"type":"verifying-key","publicKey":"pQEB..."}</c>.
    /// </summary>
    public static string VerifyingKey(string verifyingKey) =>
        Serialize(new { type = VerifyingKeyType, publicKey = verifyingKey });

    /// <summary>
    /// A private key sealed by the encryption root, with its public half.
    /// </summary>
    public static string PrivateKey(string wrappedKey, string publicKey, string? signedKey) =>
        Serialize(new { type = "legacy-compat-private-key-envelope", wrappedKey, publicKey, signedKey });

    /// <summary>
    /// A signing key sealed by the encryption root, with its verifying key.
    /// </summary>
    public static string SigningKey(string wrappedKey, string verifyingKey) =>
        Serialize(new { type = "legacy-compat-signing-key-envelope", wrappedKey, publicKey = verifyingKey });

    /// <summary>
    /// The encryption root sealed by the master password, with the KDF and salt that derive it.
    /// </summary>
    public static string MasterPassword(User user) =>
        Serialize(new { type = "raw", wrappedKey = user.Key, kdf = Kdf(user), salt = user.GetMasterPasswordSalt() });

    /// <summary>
    /// The encryption root sealed by the Key Connector key.
    /// </summary>
    public static string KeyConnector(string wrappedKey) => Serialize(new { wrappedKey });

    /// <summary>
    /// The encryption root sealed for a device or passkey key pair, with that pair.
    /// </summary>
    public static string KeyPairUnlock(string wrappedKey, string wrappedPrivateKey, string wrappedPublicKey) =>
        Serialize(new { wrappedKey, wrappedPrivateKey, wrappedPublicKey });

    private static object Kdf(User user) => user.Kdf switch
    {
        KdfType.Argon2id => new
        {
            type = Argon2id,
            iterations = user.KdfIterations,
            memory = user.KdfMemory,
            parallelism = user.KdfParallelism,
        },
        KdfType.PBKDF2_SHA256 => new { type = Pbkdf2, iterations = user.KdfIterations },
        _ => throw new InvalidOperationException($"Unknown KDF {user.Kdf}."),
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, _options);
}
