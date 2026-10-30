using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public class UserVerificationOtpStore(
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider) : IUserVerificationOtpStore
{
    private const string TokenProviderName = "UserVerification";
    private const string Purpose = "EmailOtp";
    private const int MaxFailedAttempts = 5;

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider = otpTokenProvider;

    /// <inheritdoc />
    public async Task<string> IssueAsync(User user)
    {
        // The provider returns null only for an empty provider name, purpose, or unique identifier, and none of
        // these can be empty here.
        return await _otpTokenProvider.GenerateTokenAsync(
                TokenProviderName, Purpose, UniqueIdentifier(user), user.Email)
            ?? throw new InvalidOperationException("The OTP provider did not generate an OTP.");
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeAsync(User user, string? otp)
    {
        return _otpTokenProvider.ValidateTokenAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), user.Email);
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeWithFailureLimitAsync(User user, string? otp)
    {
        return _otpTokenProvider.ValidateTokenWithFailureLimitAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), MaxFailedAttempts, user.Email);
    }

    /// <summary>
    /// Keyed by user and security stamp, like other email OTP cache keys, so a security-stamp-changing event
    /// (e.g. a password change) invalidates a pending OTP.
    /// </summary>
    private static string UniqueIdentifier(User user)
    {
        return $"{user.Id}_{user.SecurityStamp}";
    }
}
