using Bitwarden.Server.Sdk.Crypto;

namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Reads the key ids of stored keys, which the key columns don't hold.
/// </summary>
public interface IKeyIdReader
{
    /// <summary>
    /// Returns the key id of a base64 DER SPKI public key.
    /// </summary>
    string ReadPublicKeyId(string publicKey);

    /// <summary>
    /// Returns the key id of a base64 COSE verifying key.
    /// </summary>
    string ReadVerifyingKeyId(string verifyingKey);
}

/// <summary>
/// Reads key ids through the Rust SDK, which derives an RSA public key's id from its thumbprint.
/// </summary>
public class SdkKeyIdReader(ICryptoParsingClient cryptoParsingClient) : IKeyIdReader
{
    public string ReadPublicKeyId(string publicKey) =>
        cryptoParsingClient.InspectPublicKey(publicKey).KeyId;

    public string ReadVerifyingKeyId(string verifyingKey) =>
        cryptoParsingClient.InspectVerifyingKey(verifyingKey).KeyId;
}

/// <summary>
/// Turns key ids into resource ids.
/// </summary>
public static class KeyIds
{
    /// <summary>
    /// The key id that marks the end of an owner's root history.
    /// </summary>
    public const string HistoryMarker = "00000000000000000000000000000000";

    /// <summary>
    /// Returns the resource id of a key: its 32 hex key id read as a GUID, e.g.
    /// <c>c47b...</c> to <c>c47b....-....</c>. Stable, since the key columns hold no row id per key.
    /// </summary>
    public static Guid ToResourceId(string keyId) => Guid.ParseExact(keyId, "N");
}
