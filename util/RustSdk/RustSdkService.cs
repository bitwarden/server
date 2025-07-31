using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Bit.RustSDK;

public class UserKeys
{
    public required string MasterPasswordHash { get; set; }
    public required string EncryptedUserKey { get; set; }
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

    /// <summary>
    /// Hashes a password using the native implementation
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>The hashed password as a string</returns>
    /// <exception cref="ArgumentNullException">Thrown when email or password is null</exception>
    /// <exception cref="ArgumentException">Thrown when email or password is empty</exception>
    /// <exception cref="RustSdkException">Thrown when the native operation fails</exception>
    public unsafe string HashPassword(string email, string password)
    {
        // Convert strings to null-terminated byte arrays
        var emailBytes = StringToRustString(email);
        var passwordBytes = StringToRustString(password);

        try
        {
            fixed (byte* emailPtr = emailBytes)
            fixed (byte* passwordPtr = passwordBytes)
            {
                var resultPtr = NativeMethods.hash_password(emailPtr, passwordPtr);

                var result = TakeAndDestroyRustString(resultPtr);

                return result;
            }
        }
        catch (RustSdkException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (Exception ex)
        {
            throw new RustSdkException($"Failed to hash password: {ex.Message}", ex);
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
