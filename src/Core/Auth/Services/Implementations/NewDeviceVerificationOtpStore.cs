using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public class NewDeviceVerificationOtpStore : INewDeviceVerificationOtpStore
{
    private const string TokenProviderName = "NewDeviceVerification";
    private const string Purpose = "NewDeviceVerificationCode";

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider;

    public NewDeviceVerificationOtpStore(IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider)
    {
        _otpTokenProvider = otpTokenProvider;
    }

    /// <inheritdoc />
    public async Task<string> IssueAsync(User user, string deviceIdentifier)
    {
        var code = await _otpTokenProvider.GenerateTokenAsync(
            TokenProviderName, Purpose, UniqueIdentifier(user), deviceIdentifier);
        return code!;
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeAsync(User user, string deviceIdentifier, string? otp)
    {
        return _otpTokenProvider.ValidateTokenAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), deviceIdentifier);
    }

    // TODO: PM-43465 - Delete this method once every supported client version sends the Device-Identifier
    // header on the new device verification resend request. It exists only to let that resend fall back to
    // the device a pending code was issued to.
    /// <inheritdoc />
    public Task<string?> GetPendingDeviceIdentifierAsync(User user)
    {
        return _otpTokenProvider.PeekBoundValueAsync(TokenProviderName, Purpose, UniqueIdentifier(user));
    }

    /// <summary>
    /// Keyed by user and security stamp, exactly like <see cref="EmailTokenProvider"/>'s own cache key, so a
    /// security-stamp-changing event (e.g. a password change) invalidates any pending code the same way it
    /// would for other email tokens.
    /// </summary>
    private static string UniqueIdentifier(User user)
    {
        return $"{user.Id}_{user.SecurityStamp}";
    }
}
