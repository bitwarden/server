using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class SetInitialMasterPasswordCommandV1(
    ILogger<SetInitialMasterPasswordCommandV1> logger,
    IdentityErrorDescriber identityErrorDescriber,
    IUserService userService,
    IUserRepository userRepository,
    IEventService eventService,
    IAcceptOrgUserCommand acceptOrgUserCommand,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository) : ISetInitialMasterPasswordCommandV1
{
    private readonly ILogger<SetInitialMasterPasswordCommandV1> _logger = logger;
    private readonly IdentityErrorDescriber _identityErrorDescriber = identityErrorDescriber;
    private readonly IUserService _userService = userService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IEventService _eventService = eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository = organizationUserRepository;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand = acceptOrgUserCommand;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;

    public async Task<IdentityResult> SetInitialMasterPasswordAsync(User user, string masterPassword, string key,
        string orgSsoIdentifier, bool acceptOrganizationInvite = true)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!string.IsNullOrWhiteSpace(user.MasterPassword))
        {
            _logger.LogWarning("Change password failed for user {userId} - already has password.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.UserAlreadyHasPassword());
        }

        var result = await _userService.UpdatePasswordHash(user, masterPassword, validatePassword: true, refreshStamp: false);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.Key = key;

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        if (string.IsNullOrWhiteSpace(orgSsoIdentifier))
        {
            throw new BadRequestException("Organization SSO Identifier required.");
        }

        var org = await _organizationRepository.GetByIdentifierAsync(orgSsoIdentifier)
            ?? throw new BadRequestException("Organization invalid.");

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id)
            ?? throw new BadRequestException("User not found within organization.");

        // TDE users who go from a user without admin acct recovery permission to having it will be
        // required to set a MP for the first time and we don't want to re-execute the accept logic
        // as they are already confirmed.
        // TLDR: only accept post SSO user if they are invited
        if (acceptOrganizationInvite && orgUser.Status == OrganizationUserStatusType.Invited)
        {
            await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
        }

        return IdentityResult.Success;
    }
}
