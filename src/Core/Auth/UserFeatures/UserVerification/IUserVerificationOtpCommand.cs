using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.UserVerification;

/// <summary>
/// Sends and verifies the OTP that proves a user controls the mailbox at their account email.
/// </summary>
public interface IUserVerificationOtpCommand
{
    /// <summary>
    /// Emails the user a new OTP, replacing any OTP already pending.
    /// </summary>
    /// <param name="user">The authenticated user requesting the OTP.</param>
    /// <exception cref="BadRequestException">
    /// The user has no email, or the user has requested too many OTPs recently.
    /// </exception>
    Task SendOtpAsync(User user);

    /// <summary>
    /// Validates an OTP sent by <see cref="SendOtpAsync"/>. A valid OTP is consumed and marks the user's email
    /// as verified. Wrong OTPs are counted, and the pending OTP is removed when the count reaches the limit.
    /// </summary>
    /// <param name="user">The authenticated user submitting the OTP.</param>
    /// <param name="otp">The OTP the user entered.</param>
    /// <returns><see langword="true"/> if the OTP is valid; otherwise <see langword="false"/>.</returns>
    Task<bool> VerifyOtpAsync(User user, string otp);
}
