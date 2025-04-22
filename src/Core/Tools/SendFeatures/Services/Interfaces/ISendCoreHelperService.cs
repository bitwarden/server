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
    /// <param name="length"></param>
    /// <param name="useUpperCase"></param>
    /// <param name="useSpecial"></param>
    /// <returns></returns>
    string SecureRandomString(int length, bool useUpperCase, bool useSpecial);
}
