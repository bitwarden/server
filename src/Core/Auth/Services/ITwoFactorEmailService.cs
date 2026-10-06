using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface ITwoFactorEmailService
{
    /// <summary>
    /// Emails the user a login two-factor code that only the requesting device can redeem. At most one login code
    /// is live per user: issuing a new one, for any device, replaces the previous one.
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <param name="deviceIdentifier">Identifier of the device the code is being issued for</param>
    /// <exception cref="ArgumentException">Thrown if the device identifier is not provided</exception>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email for email 2FA</exception>
    Task SendTwoFactorLoginEmailAsync(User user, string deviceIdentifier);

    /// <summary>
    /// Emails the user a code for setting up email two-factor, bound to the given device. Setup codes are kept
    /// apart from login codes, so neither can be redeemed in place of the other.
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <param name="deviceIdentifier">
    /// Identifier of the device the code is being issued for. <see langword="null"/> binds the code to no device,
    /// and then only a verification without a device identifier succeeds.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email for email 2FA</exception>
    Task SendTwoFactorSetupEmailAsync(User user, string? deviceIdentifier);

    /// <summary>
    /// Sends a new device verification email to the user with an OTP token that only the requesting device
    /// can redeem. At most one such code is ever live per user - issuing a new one, for any device,
    /// invalidates whichever code came before it.
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

    /// <summary>
    /// Verifies a login two-factor code against the device it was issued for. A code issued for a different
    /// device does not verify, even when the code itself is correct. A valid code is consumed.
    /// </summary>
    /// <param name="user">The user attempting to log in</param>
    /// <param name="deviceIdentifier">Identifier of the device submitting the code; a blank value never verifies</param>
    /// <param name="token">The code to verify; a blank code never verifies</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    Task<bool> VerifyTwoFactorLoginTokenAsync(User user, string? deviceIdentifier, string? token);

    /// <summary>
    /// Verifies an email two-factor setup code against the device it was issued for. A valid code is consumed.
    /// </summary>
    /// <param name="user">The user setting up email two-factor</param>
    /// <param name="deviceIdentifier">Identifier of the device submitting the code; must match the one at issue</param>
    /// <param name="token">The code to verify; a blank code never verifies</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    Task<bool> VerifyTwoFactorSetupTokenAsync(User user, string? deviceIdentifier, string? token);
}
