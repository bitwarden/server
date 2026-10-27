using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.UserVerification;

public class UserVerificationOtpCommand(
    IUserVerificationOtpStore userVerificationOtpStore,
    IMailService mailService,
    IUserRepository userRepository) : IUserVerificationOtpCommand
{
    private readonly IUserVerificationOtpStore _userVerificationOtpStore = userVerificationOtpStore;
    private readonly IMailService _mailService = mailService;
    private readonly IUserRepository _userRepository = userRepository;

    /// <inheritdoc />
    public async Task SendOtpAsync(User user)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            throw new BadRequestException("No user email.");
        }

        var otp = await _userVerificationOtpStore.IssueAsync(user)
            ?? throw new BadRequestException("Too many verification codes requested. Try again later.");

        await _mailService.SendOTPEmailAsync(user.Email, otp);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyOtpAsync(User user, string otp)
    {
        if (!await _userVerificationOtpStore.ValidateAndConsumeWithFailureLimitAsync(user, otp))
        {
            return false;
        }

        // A valid emailed OTP proves mailbox control.
        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);
        }

        return true;
    }
}
