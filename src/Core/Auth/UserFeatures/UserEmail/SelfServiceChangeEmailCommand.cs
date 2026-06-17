using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public class SelfServiceChangeEmailCommand(
    IUserService userService,
    UserManager<User> userManager,
    IUserRepository userRepository,
    IMailService mailService,
    IOrganizationDomainAllowEmailChangeQuery organizationDomainAllowEmailChangeQuery,
    IPushNotificationService pushService,
    IChangeEmailCommand changeEmailCommand,
    IOptions<IdentityOptions> identityOptions) : ISelfServiceChangeEmailCommand
{
    private readonly IUserService _userService = userService;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly UserManager<User> _userManager = userManager;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IMailService _mailService = mailService;
    private readonly IOrganizationDomainAllowEmailChangeQuery _organizationDomainAllowEmailChangeQuery = organizationDomainAllowEmailChangeQuery;
    private readonly IChangeEmailCommand _changeEmailCommand = changeEmailCommand;
    private readonly IdentityOptions _identityOptions = identityOptions.Value;

    /// <inheritdoc />
    public async Task ChangeEmailAsync(User user, string masterPassword, string newEmail, string token)
    {
        await CheckPasswordAndKeyConnector(user, masterPassword);

        var changeEmailTokenIsValid = await _userManager.VerifyUserTokenAsync(
            user,
            _identityOptions.Tokens.ChangeEmailTokenProvider,
            GetChangeEmailTokenPurpose(newEmail),
            token);

        if (!changeEmailTokenIsValid)
        {
            throw new BadRequestException("Token", "Invalid token.");
        }

        await _changeEmailCommand.ChangeEmailAsync(user, newEmail);

        await _pushService.PushSyncSettingsAsync(user.Id);

        return;
    }

    /// <inheritdoc />
    public async Task InitiateChangeEmailAsync(User user, string masterPassword, string newEmail)
    {
        await CheckPasswordAndKeyConnector(user, masterPassword);

        await _organizationDomainAllowEmailChangeQuery.ValidateAllowedAsync(user, newEmail);

        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null)
        {
            // Mirrors the legacy UserService flow: avoid leaking that the address is taken by
            // notifying the current address instead of returning a hard error to the caller.
            await _mailService.SendChangeEmailAlreadyExistsEmailAsync(user.Email, newEmail);
            return;
        }

        var token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        await _mailService.SendChangeEmailEmailAsync(newEmail, token);

        return;
    }

    // Mirrors the private purpose string used by ASP.NET Core Identity's
    // UserManager.GenerateChangeEmailTokenAsync so VerifyUserTokenAsync accepts tokens issued
    // through the standard UserManager flow.
    private static string GetChangeEmailTokenPurpose(string newEmail) => "ChangeEmail:" + newEmail;

    private async Task CheckPasswordAndKeyConnector(User user, string masterPassword)
    {
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot change your email when using Key Connector.");
        }

        if (!await _userService.CheckPasswordAsync(user, masterPassword))
        {
            throw new BadRequestException("MasterPasswordHash", "Invalid password.");
        }
    }
}
