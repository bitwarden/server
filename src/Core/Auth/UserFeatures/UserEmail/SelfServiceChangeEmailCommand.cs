using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class SelfServiceChangeEmailCommand(
    IUserService userService,
    UserManager<User> userManager,
    IChangeEmailCommand changeEmailCommand,
    IdentityErrorDescriber identityErrorDescriber,
    IOptions<IdentityOptions> identityOptions) : ISelfServiceChangeEmailCommand
{
    private readonly IUserService _userService = userService;
    private readonly UserManager<User> _userManager = userManager;
    private readonly IChangeEmailCommand _changeEmailCommand = changeEmailCommand;
    private readonly IdentityErrorDescriber _identityErrorDescriber = identityErrorDescriber;
    private readonly IdentityOptions _identityOptions = identityOptions.Value;

    /// <inheritdoc />
    public async Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string token)
    {
        if (!await _userService.CheckPasswordAsync(user, masterPassword))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        var changeEmailTokenIsValid = await _userManager.VerifyUserTokenAsync(
            user,
            _identityOptions.Tokens.ChangeEmailTokenProvider,
            GetChangeEmailTokenPurpose(newEmail),
            token);
        if (!changeEmailTokenIsValid)
        {
            return IdentityResult.Failed(_identityErrorDescriber.InvalidToken());
        }

        await _changeEmailCommand.ChangeEmailAsync(user, newEmail);

        return IdentityResult.Success;
    }

    // Mirrors the private purpose string used by ASP.NET Core Identity's
    // UserManager.GenerateChangeEmailTokenAsync so VerifyUserTokenAsync accepts tokens issued
    // through the standard UserManager flow.
    private static string GetChangeEmailTokenPurpose(string newEmail) => "ChangeEmail:" + newEmail;
}
