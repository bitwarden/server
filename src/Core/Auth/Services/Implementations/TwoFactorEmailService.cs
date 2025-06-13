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

    public async Task SendTwoFactorEmailAsync(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.TryGetValue("Email", out var emailValue))
        {
            throw new ArgumentNullException("No email.");
        }

        var email = ((string)emailValue).ToLowerInvariant();
        var token = await _userManager.GenerateTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email));

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            email, user.Email, token, _currentContext.IpAddress, deviceType, TwoFactorEmailPurpose.Login);
    }

    public async Task SendTwoFactorSetupEmailAsync(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.TryGetValue("Email", out var emailValue))
        {
            throw new ArgumentNullException("No email.");
        }

        var email = ((string)emailValue).ToLowerInvariant();
        var token = await _userManager.GenerateTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email));

        var deviceType = _currentContext.DeviceType?.GetType().GetMember(_currentContext.DeviceType?.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? "Unknown Browser";

        await _mailService.SendTwoFactorEmailAsync(
            email, user.Email, token, _currentContext.IpAddress, deviceType, TwoFactorEmailPurpose.Setup);
    }

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

    public async Task<bool> VerifyTwoFactorEmailAsync(User user, string token)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.TryGetValue("Email", out var emailValue))
        {
            throw new ArgumentNullException("No email.");
        }

        var email = ((string)emailValue).ToLowerInvariant();
        return await _userManager.VerifyTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), token);
    }
}
