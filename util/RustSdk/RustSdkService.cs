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
/// Service implementation that provides a C# friendly interface to the Rust SDK
/// </summary>
public class RustSdkService
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public unsafe UserKeys GenerateUserKeys(string email, string password)
    {
        var emailBytes = StringToRustString(email);
        var passwordBytes = StringToRustString(password);

        fixed (byte* emailPtr = emailBytes)
        fixed (byte* passwordPtr = passwordBytes)
        {
            var resultPtr = NativeMethods.generate_user_keys(emailPtr, passwordPtr);

            var result = ParseResponse(resultPtr);

            return JsonSerializer.Deserialize<UserKeys>(result, CaseInsensitiveOptions)!;
        }
    }

    public unsafe OrganizationKeys GenerateOrganizationKeys()
    {
        var resultPtr = NativeMethods.generate_organization_keys();

        var result = ParseResponse(resultPtr);

        return JsonSerializer.Deserialize<OrganizationKeys>(result, CaseInsensitiveOptions)!;
    }

    public unsafe string GenerateUserOrganizationKey(string userKey, string orgKey)
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

    public unsafe string EncryptCipher(string cipherViewJson, string symmetricKeyBase64)
    {
        var cipherViewBytes = StringToRustString(cipherViewJson);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* cipherViewPtr = cipherViewBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.encrypt_cipher(cipherViewPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    public unsafe string DecryptCipher(string cipherJson, string symmetricKeyBase64)
    {
        var cipherBytes = StringToRustString(cipherJson);
        var keyBytes = StringToRustString(symmetricKeyBase64);

        fixed (byte* cipherPtr = cipherBytes)
        fixed (byte* keyPtr = keyBytes)
        {
            var resultPtr = NativeMethods.decrypt_cipher(cipherPtr, keyPtr);

            return ParseResponse(resultPtr);
        }
    }

    /// <summary>
    /// Encrypts a plaintext string using the provided symmetric key.
    /// Returns an EncString in format "2.{iv}|{data}|{mac}".
    /// </summary>
    public unsafe string EncryptString(string plaintext, string symmetricKeyBase64)
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
