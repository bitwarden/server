// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Core.Auth.Enums;

namespace Bit.Core.Auth.Services;

public class TwoFactorEmailService : ITwoFactorEmailService
{
    private const string TokenProviderName = "TwoFactorEmail";
    private const string LoginPurpose = "LoginCode";
    private const string SetupPurpose = "SetupCode";

    private readonly ICurrentContext _currentContext;
    private readonly IMailService _mailService;
    private readonly INewDeviceVerificationOtpStore _newDeviceVerificationOtpStore;
    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider;

    public TwoFactorEmailService(
        ICurrentContext currentContext,
        IMailService mailService,
        INewDeviceVerificationOtpStore newDeviceVerificationOtpStore,
        IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider
    )
    {
        _currentContext = currentContext;
        _mailService = mailService;
        _newDeviceVerificationOtpStore = newDeviceVerificationOtpStore;
        _otpTokenProvider = otpTokenProvider;
    }

    /// <inheritdoc />
    public async Task SendTwoFactorLoginEmailAsync(User user, string deviceIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceIdentifier);

        await VerifyAndSendTwoFactorEmailAsync(user, LoginPurpose, deviceIdentifier, TwoFactorEmailPurpose.Login);
    }

    /// <inheritdoc />
    public async Task SendTwoFactorSetupEmailAsync(User user, string deviceIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceIdentifier);

        await VerifyAndSendTwoFactorEmailAsync(user, SetupPurpose, deviceIdentifier, TwoFactorEmailPurpose.Setup);
    }

    /// <inheritdoc />
    public async Task SendNewDeviceVerificationEmailAsync(User user, string deviceIdentifier)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceIdentifier);

        var code = await _newDeviceVerificationOtpStore.IssueAsync(user, deviceIdentifier);

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            user.Email, user.Email, code, _currentContext.IpAddress, deviceType, TwoFactorEmailPurpose.NewDeviceVerification);
    }

    // TODO: PM-43465 - Delete this method once every supported client version sends the Device-Identifier
    // header on the new device verification resend request.
    /// <inheritdoc />
    public async Task<string> GetPendingNewDeviceVerificationDeviceIdentifierAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return await _newDeviceVerificationOtpStore.GetPendingDeviceIdentifierAsync(user);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyNewDeviceVerificationOtpAsync(User user, string deviceIdentifier, string otp)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceIdentifier);

        return await _newDeviceVerificationOtpStore.ValidateAndConsumeAsync(user, deviceIdentifier, otp);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTwoFactorLoginTokenAsync(User user, string deviceIdentifier, string token)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return await _otpTokenProvider.ValidateTokenAsync(
            token, TokenProviderName, LoginPurpose, UniqueIdentifier(user), deviceIdentifier);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTwoFactorSetupTokenAsync(User user, string deviceIdentifier, string token)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return await _otpTokenProvider.ValidateTokenAsync(
            token, TokenProviderName, SetupPurpose, UniqueIdentifier(user), deviceIdentifier);
    }

    /// <summary>
    /// Issues a code bound to the given device and emails it to the user's two-factor email address, only if
    /// they have one.
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <param name="otpPurpose">Which code to issue; login and setup codes are stored apart</param>
    /// <param name="deviceIdentifier">The device the code is bound to</param>
    /// <param name="emailPurpose">The purpose of the email</param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email set up for 2FA</exception>
    private async Task VerifyAndSendTwoFactorEmailAsync(
        User user, string otpPurpose, string deviceIdentifier, TwoFactorEmailPurpose emailPurpose)
    {
        var email = GetUserTwoFactorEmail(user);
        var token = await _otpTokenProvider.GenerateTokenAsync(
            TokenProviderName, otpPurpose, UniqueIdentifier(user), deviceIdentifier);

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            email, user.Email, token, _currentContext.IpAddress, deviceType, emailPurpose);
    }

    /// <summary>
    /// Keyed by user and security stamp, so a security-stamp-changing event (e.g. a password change) invalidates
    /// any pending code.
    /// </summary>
    private static string UniqueIdentifier(User user)
    {
        return $"{user.Id}_{user.SecurityStamp}";
    }

    /// <summary>
    ///  Verifies the user has email 2FA and will return the email if present and throw otherwise.
    /// </summary>
    /// <param name="user">The user to check</param>
    /// <returns>The user's 2FA email address</returns>
    /// <exception cref="ArgumentNullException"></exception>
    private string GetUserTwoFactorEmail(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.TryGetValue("Email", out var emailValue)
            || string.IsNullOrWhiteSpace((string)emailValue))
        {
            throw new ArgumentNullException("No email.");
        }
        return ((string)emailValue).ToLowerInvariant();
    }
}
