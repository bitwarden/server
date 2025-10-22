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

            var result = TakeAndDestroyRustString(resultPtr);

            return JsonSerializer.Deserialize<UserKeys>(result, CaseInsensitiveOptions)!;
        }
    }

    public unsafe OrganizationKeys GenerateOrganizationKeys()
    {
        var resultPtr = NativeMethods.generate_organization_keys();

        var result = TakeAndDestroyRustString(resultPtr);

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

            var result = TakeAndDestroyRustString(resultPtr);

            return result;
        }
    }


    private static byte[] StringToRustString(string str)
    {
        return Encoding.UTF8.GetBytes(str + '\0');
    }

    private static unsafe string TakeAndDestroyRustString(byte* ptr)
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

        return result;
    }
}
