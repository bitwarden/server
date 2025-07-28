using System.Runtime.InteropServices;
using System.Text;

namespace Bit.RustSDK;

/// <summary>
/// Service implementation that provides a C# friendly interface to the Rust SDK
/// </summary>
public class RustSdkService
{
    /// <summary>
    /// Adds two integers using the native implementation
    /// </summary>
    /// <param name="x">First integer</param>
    /// <param name="y">Second integer</param>
    /// <returns>The sum of x and y</returns>
    public int Add(int x, int y)
    {
        try
        {
            return NativeMethods.my_add(x, y);
        }
        catch (Exception ex)
        {
            throw new RustSdkException($"Failed to perform addition operation: {ex.Message}", ex);
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
        var emailBytes = Encoding.UTF8.GetBytes(email + '\0');
        var passwordBytes = Encoding.UTF8.GetBytes(password + '\0');

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
