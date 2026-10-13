using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

/// <summary>
/// Holds the single new device verification code currently pending for a user. Issuing a code for any
/// device replaces whichever code was pending before it, so at most one is ever redeemable at a time.
/// </summary>
public interface INewDeviceVerificationOtpStore
{
    /// <summary>
    /// Generates a code, binds it to the given device, and replaces any code already pending for the user.
    /// </summary>
    /// <param name="user">The user the code is being issued for</param>
    /// <param name="deviceIdentifier">Identifier of the device the code is being issued for</param>
    /// <returns>The generated code</returns>
    Task<string> IssueAsync(User user, string deviceIdentifier);

    /// <summary>
    /// Validates a code against the device it was issued for. A code issued for a different device identifier
    /// does not validate, even when the code itself is correct. A valid code is consumed and cannot be
    /// redeemed again.
    /// </summary>
    /// <param name="user">The user attempting verification</param>
    /// <param name="deviceIdentifier">Identifier of the device submitting the code</param>
    /// <param name="otp">The code to validate; an empty or missing code is treated as incorrect</param>
    Task<bool> ValidateAndConsumeAsync(User user, string deviceIdentifier, string? otp);

    // TODO: PM-43465 - Delete this member and its implementation once every supported client version sends
    // the Device-Identifier header on the new device verification resend request. It exists only to let that
    // resend fall back to the device a pending code was issued to.
    /// <summary>
    /// Returns the device identifier the currently pending code was issued for, or <see langword="null"/> if
    /// no code is pending.
    /// </summary>
    /// <param name="user">The user whose pending code is being looked up</param>
    Task<string?> GetPendingDeviceIdentifierAsync(User user);
}
