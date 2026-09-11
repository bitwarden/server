using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface ITwoFactorEmailService
{
    Task SendTwoFactorEmailAsync(User user);
    Task SendTwoFactorSetupEmailAsync(User user);

    /// <summary>
    /// Sends a new device verification email to the user with an OTP token that only the requesting device
    /// can redeem.
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <param name="deviceIdentifier">Identifier of the device the code is being issued for</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    /// <exception cref="ArgumentException">Thrown if the device identifier is not provided</exception>
    Task SendNewDeviceVerificationEmailAsync(User user, string deviceIdentifier);

    /// <summary>
    /// Verifies a new device verification OTP against the device it was issued for. A code issued for a
    /// different device identifier does not verify, even when the code itself is correct.
    /// </summary>
    /// <param name="user">The user attempting verification</param>
    /// <param name="deviceIdentifier">Identifier of the device submitting the code</param>
    /// <param name="otp">The OTP to verify; an empty OTP is treated as an incorrect OTP</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    /// <exception cref="ArgumentException">Thrown if the device identifier is not provided</exception>
    Task<bool> VerifyNewDeviceVerificationOtpAsync(User user, string deviceIdentifier, string otp);

    // TODO: PM-43465 - Delete this member and its implementation once every supported client version sends
    // the Device-Identifier header on the new device verification resend request.
    /// <summary>
    /// Returns the device the most recent new device verification code was issued to, or <see langword="null"/>
    /// when no code has been issued within the record's lifetime. Lets a resend be scoped to the device that
    /// was originally challenged when the resend request does not identify a device itself.
    /// </summary>
    /// <param name="user">The user whose pending verification is being looked up</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    Task<string?> GetPendingNewDeviceVerificationDeviceIdentifierAsync(User user);

    Task<bool> VerifyTwoFactorTokenAsync(User user, string token);
}
