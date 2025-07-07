namespace Core.Auth.Identity.TokenProviders;

public interface IOtpTokenProvider
{
    /// <summary>
    /// Generates a one-time password (OTP) for the specified user.
    /// </summary>
    /// <returns>The generated OTP as a string.</returns>
    Task<string> GenerateTokenAsync();

    /// <summary>
    /// Validates the provided OTP for the specified user.
    /// </summary>
    /// <param name="token">The OTP to validate.</param>
    /// <returns>True if the OTP is valid; otherwise, false.</returns>
    Task<bool> ValidateTokenAsync(string token);
}
