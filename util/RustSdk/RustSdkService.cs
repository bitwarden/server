using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Bit.RustSDK;

public class UserKeys
{
    public required string MasterPasswordHash { get; set; }
    /// <summary>
    /// Base64 encoded UserKey
    /// </summary>
    public required string Key { get; set; }
    public required string EncryptedUserKey { get; set; }
    public required string PublicKey { get; set; }
    public required string PrivateKey { get; set; }
}

public class OrganizationKeys
{
    /// <summary>
    /// Base64 encoded SymmetricCryptoKey
    /// </summary>
    public required string Key { get; set; }

    public required string PublicKey { get; set; }
    public required string PrivateKey { get; set; }
}

/// <summary>
/// The result of encrypting an attachment: the encrypted metadata plus the EncArrayBuffer blob to store.
/// </summary>
public class EncryptedAttachment
{
    /// <summary>
    /// The wrapped attachment key (EncString), or <c>null</c> for legacy attachments that have no attachment key.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>Encrypted filename (EncString).</summary>
    public required string FileName { get; set; }

    /// <summary>The encrypted file bytes in EncArrayBuffer binary layout, to be written to attachment storage.</summary>
    public required byte[] Data { get; set; }

    /// <summary>The encrypted blob byte length (equals <see cref="Data"/>.Length).</summary>
    public long Size { get; set; }
}

/// <summary>
/// Service implementation that provides a C# friendly interface to the Rust SDK
/// </summary>
public class RustSdkService
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class AttachmentResult
    {
        public string? Key { get; init; }

        public string FileName { get; init; } = string.Empty;

        public string Blob { get; init; } = string.Empty;

        public long Size { get; init; }
    }

    public static unsafe UserKeys GenerateUserKeys(string email, string password, int kdfIterations = 5_000, uint poolIndex = 0)
    {
        var emailBytes = StringToRustString(email);
        var passwordBytes = StringToRustString(password);

        fixed (byte* emailPtr = emailBytes)
        fixed (byte* passwordPtr = passwordBytes)
        {
            var resultPtr = NativeMethods.generate_user_keys(emailPtr, passwordPtr, (uint)kdfIterations, poolIndex);

            var result = ParseResponse(resultPtr);

            return JsonSerializer.Deserialize<UserKeys>(result, CaseInsensitiveOptions)!;
        }
    }

    public static unsafe OrganizationKeys GenerateOrganizationKeys()
    {
        var resultPtr = NativeMethods.generate_organization_keys();

        var result = ParseResponse(resultPtr);

        return JsonSerializer.Deserialize<OrganizationKeys>(result, CaseInsensitiveOptions)!;
    }

    public static unsafe string GenerateUserOrganizationKey(string userKey, string orgKey)
    {
        var userKeyBytes = StringToRustString(userKey);
        var orgKeyBytes = StringToRustString(orgKey);

        fixed (byte* userKeyPtr = userKeyBytes)
        fixed (byte* orgKeyPtr = orgKeyBytes)
        {
            var resultPtr = NativeMethods.generate_user_organization_key(userKeyPtr, orgKeyPtr);

            var result = ParseResponse(resultPtr);

            return result;
        }
    }

    /// <summary>
    /// Encrypts a plaintext string using the provided symmetric key.
    /// Returns an EncString in format "2.{iv}|{data}|{mac}".
    /// </summary>
    public static unsafe string EncryptString(string plaintext, string symmetricKeyBase64)
    {
        var plaintextBytes = StringToRustString(plaintext);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* plaintextPtr = plaintextBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.encrypt_string(plaintextPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    /// <summary>
    /// Wraps a symmetric key with another symmetric key, returning an EncString in format
    /// "2.{iv}|{data}|{mac}" whose plaintext is the raw key bytes (so it unwraps back into a
    /// symmetric key). Use this for keys a client unwraps via <c>unwrap_symmetric_key</c> — e.g. a
    /// <c>ProviderOrganization.Key</c> (an organization key wrapped with the provider's symmetric key).
    /// Unlike <see cref="EncryptString"/>, this encrypts the key bytes, not the base64 text.
    /// </summary>
    public static unsafe string WrapSymmetricKey(string keyToWrapBase64, string wrappingKeyBase64)
    {
        var keyToWrapBytes = StringToRustString(keyToWrapBase64);
        var wrappingKeyBytes = StringToRustString(wrappingKeyBase64);

        fixed (byte* keyToWrapPtr = keyToWrapBytes)
        fixed (byte* wrappingKeyPtr = wrappingKeyBytes)
        {
            var resultPtr = NativeMethods.wrap_symmetric_key(keyToWrapPtr, wrappingKeyPtr);

            return ParseResponse(resultPtr);
        }
    }

    /// <summary>
    /// Decrypts an EncString using the provided symmetric key.
    /// </summary>
    public static unsafe string DecryptString(string encString, string symmetricKeyBase64)
    {
        var encStringBytes = StringToRustString(encString);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* encStringPtr = encStringBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.decrypt_string(encStringPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    /// <summary>
    /// Encrypts specified fields in a JSON object. Field paths use dot notation
    /// with [*] for array elements (e.g. "login.uris[*].uri").
    /// Returns the modified JSON with matching string fields encrypted as EncStrings.
    /// </summary>
    public static unsafe string EncryptFields(string json, string fieldPathsJson, string symmetricKeyBase64)
    {
        var jsonBytes = StringToRustString(json);
        var pathsBytes = StringToRustString(fieldPathsJson);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* jsonPtr = jsonBytes)
        fixed (byte* pathsPtr = pathsBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.encrypt_fields(jsonPtr, pathsPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    /// <summary>
    /// Encrypts an attachment's file bytes and filename in one of Bitwarden's attachment scheme versions
    /// (v0/v1/v2). Returns the encrypted metadata plus the EncArrayBuffer blob.
    /// </summary>
    /// <param name="fileBytes">The plaintext file bytes.</param>
    /// <param name="vaultKeyBase64">Base64-encoded vault key (the user or organization symmetric key).</param>
    /// <param name="wrappedCipherKey">The cipher's wrapped <c>Key</c> EncString; required for v2, ignored otherwise.</param>
    /// <param name="fileName">The plaintext filename.</param>
    /// <param name="version">0 = v0 (no attachment key); 1 = v1 (attachment key wrapped by the vault key); 2 = v2 (attachment key wrapped by the cipher key).</param>
    public static unsafe EncryptedAttachment EncryptAttachment(
        byte[] fileBytes,
        string vaultKeyBase64,
        string? wrappedCipherKey,
        string fileName,
        uint version)
    {
        var fileBytesInput = StringToRustString(Convert.ToBase64String(fileBytes));
        var vaultKeyBytes = StringToRustString(vaultKeyBase64);
        var wrappedCipherKeyBytes = StringToRustString(wrappedCipherKey ?? string.Empty);
        var fileNameBytes = StringToRustString(fileName);

        fixed (byte* fileBytesPtr = fileBytesInput)
        fixed (byte* vaultKeyPtr = vaultKeyBytes)
        fixed (byte* wrappedCipherKeyPtr = wrappedCipherKeyBytes)
        fixed (byte* fileNamePtr = fileNameBytes)
        {
            var resultPtr = NativeMethods.encrypt_attachment(
                fileBytesPtr, vaultKeyPtr, wrappedCipherKeyPtr, fileNamePtr, version);

            var result = ParseResponse(resultPtr);

            var dto = JsonSerializer.Deserialize<AttachmentResult>(result, CaseInsensitiveOptions)
                ?? throw new RustSdkException("Failed to parse attachment encryption result");

            return new EncryptedAttachment
            {
                Key = dto.Key,
                FileName = dto.FileName,
                Data = Convert.FromBase64String(dto.Blob),
                Size = dto.Size
            };
        }
    }

    /// <summary>
    /// Encrypts specified JSON fields under a freshly generated per-cipher key and returns the modified
    /// JSON with the cipher key (wrapped by the vault key) injected as the top-level <c>key</c> field.
    /// Use this to produce a "cipher key" cipher; use <see cref="EncryptFields"/> for a user-key cipher.
    /// </summary>
    public static unsafe string EncryptFieldsWithCipherKey(string json, string fieldPathsJson, string symmetricKeyBase64)
    {
        var jsonBytes = StringToRustString(json);
        var pathsBytes = StringToRustString(fieldPathsJson);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* jsonPtr = jsonBytes)
        fixed (byte* pathsPtr = pathsBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.encrypt_fields_with_cipher_key(jsonPtr, pathsPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    private static byte[] StringToRustString(string str)
    {
        return Encoding.UTF8.GetBytes(str + '\0');
    }

    /// <summary>
    /// Parses a response from Rust FFI, checks for errors, and frees the native string.
    /// </summary>
    /// <param name="ptr">Pointer to the C string returned from Rust</param>
    /// <returns>The parsed response string</returns>
    /// <exception cref="RustSdkException">Thrown if the pointer is null, conversion fails, or the response contains an error</exception>
    private static unsafe string ParseResponse(byte* ptr)
    {
        if (ptr == null)
        {
            throw new RustSdkException("Pointer is null");
        }

        var result = Marshal.PtrToStringUTF8((IntPtr)ptr);
        NativeMethods.free_c_string(ptr);

        if (result == null)
        {
            throw new RustSdkException("Failed to convert native result to string");
        }

        // Check if response is an error from Rust
        // Rust error responses follow the format: {"error": "message"}
        if (result.TrimStart().StartsWith('{') && result.Contains("\"error\"", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(result);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.GetString();
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        throw new RustSdkException($"Rust SDK error: {errorMessage}");
                    }
                }
            }
            catch (JsonException)
            {
                // If we can't parse it as an error, it's likely valid data that happens to contain "error"
            }
        }

        return result;
    }
}
