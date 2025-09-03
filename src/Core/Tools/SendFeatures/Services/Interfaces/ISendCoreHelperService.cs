namespace Bit.Core.Tools.Services;

/// <summary>
/// This interface provides helper methods for generating secure random strings. Making
/// it easier to mock the service in unit tests.
/// </summary>
public interface ISendCoreHelperService
{
    /// <summary>
    /// Securely generates a random string of the specified length.
    /// </summary>
    /// <param name="length">Desired string length to be returned</param>
    /// <param name="useUpperCase">Desired casing for the string</param>
    /// <param name="useSpecial">Determines if special characters will be used in string</param>
    /// <returns>A secure random string with the desired parameters</returns>
    string SecureRandomString(int length, bool useUpperCase, bool useSpecial);
}
