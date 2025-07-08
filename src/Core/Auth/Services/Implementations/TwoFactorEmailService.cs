// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Core.Auth.Enums;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.Services;

public class TwoFactorEmailService : ITwoFactorEmailService
{
    private readonly ICurrentContext _currentContext;
    private readonly UserManager<User> _userManager;
    private readonly IMailService _mailService;

    public TwoFactorEmailService(
        ICurrentContext currentContext,
        IMailService mailService,
        UserManager<User> userManager
    )
    {
        _currentContext = currentContext;
        _userManager = userManager;
        _mailService = mailService;
    }

    /// <summary>
    /// Sends a two-factor email to the user with an OTP token for login
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email for email 2FA</exception>
    public async Task SendTwoFactorEmailAsync(User user)
    {
        await VerifyAndSendTwoFactorEmailAsync(user, TwoFactorEmailPurpose.Login);
    }

    /// <summary>
    /// Sends a two-factor email to the user with an OTP for setting up 2FA
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email for email 2FA</exception>
    public async Task SendTwoFactorSetupEmailAsync(User user)
    {
        await VerifyAndSendTwoFactorEmailAsync(user, TwoFactorEmailPurpose.Setup);
    }

    /// <summary>
    /// Sends a new device verification email to the user with an OTP token
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <exception cref="ArgumentNullException">Thrown if the user is not provided</exception>
    public async Task SendNewDeviceVerificationEmailAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var token = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider,
            "otp:" + user.Email);

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            user.Email, user.Email, token, _currentContext.IpAddress, deviceType, TwoFactorEmailPurpose.NewDeviceVerification);
    }

    /// <summary>
    /// Verifies the two-factor token for the specified user
    /// </summary>
    /// <param name="user">The user for whom the token should be verified</param>
    /// <param name="token">The token to verify</param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email for email 2FA</exception>
    public async Task<bool> VerifyTwoFactorTokenAsync(User user, string token)
    {
        var email = GetUserTwoFactorEmail(user);
        return await _userManager.VerifyTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), token);
    }

    /// <summary>
    /// Sends a two-factor email with the specified purpose to the user only if they have 2FA email set up
    /// </summary>
    /// <param name="user">The user to whom the email should be sent</param>
    /// <param name="purpose">The purpose of the email</param>
    /// <exception cref="ArgumentNullException">Thrown if the user does not have an email set up for 2FA</exception>
    private async Task VerifyAndSendTwoFactorEmailAsync(User user, TwoFactorEmailPurpose purpose)
    {
        var email = GetUserTwoFactorEmail(user);
        var token = await _userManager.GenerateTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email));

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            email, user.Email, token, _currentContext.IpAddress, deviceType, purpose);
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
        if (provider == null || provider.MetaData == null || !provider.MetaData.TryGetValue("Email", out var emailValue))
        {
            throw new ArgumentNullException("No email.");
        }
        return ((string)emailValue).ToLowerInvariant();
    }
}
