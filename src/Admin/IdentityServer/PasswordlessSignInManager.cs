using Bit.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bit.Admin.IdentityServer;

public class PasswordlessSignInManager<TUser> : SignInManager<TUser> where TUser : class
{
    public const string PasswordlessSignInPurpose = "PasswordlessSignIn";

    private readonly IMailService _mailService;

    public PasswordlessSignInManager(UserManager<TUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<TUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<TUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<TUser> confirmation,
        IMailService mailService)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _mailService = mailService;
    }

    public async Task<SignInResult> PasswordlessSignInAsync(string email, string returnUrl)
    {
        var user = await UserManager.FindByEmailAsync(email);
        if (user == null)
        {
            return SignInResult.Failed;
        }

        var token = await UserManager.GenerateUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider,
            PasswordlessSignInPurpose);
        await _mailService.SendPasswordlessSignInAsync(returnUrl, token, email);
        return SignInResult.Success;
    }

    public async Task<SignInResult> PasswordlessSignInAsync(TUser user, string token, bool isPersistent)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var attempt = await CheckPasswordlessSignInAsync(user, token);
        return attempt.Succeeded ?
            await SignInOrTwoFactorAsync(user, isPersistent, bypassTwoFactor: true) : attempt;
    }

    public async Task<SignInResult> PasswordlessSignInAsync(string email, string token, bool isPersistent)
    {
        var user = await UserManager.FindByEmailAsync(email);
        if (user == null)
        {
            return SignInResult.Failed;
        }

        return await PasswordlessSignInAsync(user, token, isPersistent);
    }

    public virtual async Task<SignInResult> CheckPasswordlessSignInAsync(TUser user, string token)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var error = await PreSignInCheck(user);
        if (error != null)
        {
            return error;
        }

        if (await UserManager.VerifyUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider,
            PasswordlessSignInPurpose, token))
        {
            return SignInResult.Success;
        }

        Logger.LogWarning(2, "User {userId} failed to provide the correct token.",
            await UserManager.GetUserIdAsync(user));
        return SignInResult.Failed;
    }
}
