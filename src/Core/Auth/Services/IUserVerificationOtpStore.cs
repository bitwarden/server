using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

/// <summary>
/// Holds the single user verification OTP currently pending for a user. The OTP is emailed to the user and
/// proves control of their mailbox. Issuing an OTP replaces whichever OTP was pending before it.
/// </summary>
/// <remarks>
/// The OTP is bound to the user's security stamp and email, so changing either invalidates a pending OTP.
/// </remarks>
public interface IUserVerificationOtpStore
{
    /// <summary>
    /// Generates an OTP and replaces any OTP already pending for the user. Each user can be issued a limited
    /// number of OTPs in a fixed time window.
    /// </summary>
    /// <param name="user">The user the OTP is being issued for</param>
    /// <returns>The generated OTP, or <see langword="null"/> if the user has reached the request limit</returns>
    Task<string?> IssueAsync(User user);

    // TODO: PM-<placeholder-otp-limit> - Move the step-up user verification callers to
    // ValidateAndConsumeWithFailureLimitAsync, then delete this member and its implementation.
    /// <summary>
    /// Validates an OTP. A valid OTP is consumed and cannot be redeemed again. Wrong OTPs are not counted.
    /// </summary>
    /// <param name="user">The user attempting verification</param>
    /// <param name="otp">The OTP to validate; an empty or missing OTP is treated as incorrect</param>
    Task<bool> ValidateAndConsumeAsync(User user, string? otp);

    /// <summary>
    /// Validates an OTP. A valid OTP is consumed and cannot be redeemed again. Wrong OTPs are counted, and the
    /// pending OTP is removed when the count reaches the limit. The user must then be issued a new OTP.
    /// </summary>
    /// <param name="user">The user attempting verification</param>
    /// <param name="otp">The OTP to validate; an empty or missing OTP is treated as incorrect</param>
    Task<bool> ValidateAndConsumeWithFailureLimitAsync(User user, string? otp);
}
