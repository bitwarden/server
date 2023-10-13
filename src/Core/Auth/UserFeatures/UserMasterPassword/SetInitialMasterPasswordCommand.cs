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

public class SetInitialMasterPasswordCommand : ISetInitialMasterPasswordCommand
{
    private readonly ILogger<SetInitialMasterPasswordCommand> _logger;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;


    public SetInitialMasterPasswordCommand(
        ILogger<SetInitialMasterPasswordCommand> logger,
        IdentityErrorDescriber identityErrorDescriber,
        IUserService userService,
        IUserRepository userRepository,
        IEventService eventService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        _logger = logger;
        _identityErrorDescriber = identityErrorDescriber;
        _userService = userService;
        _userRepository = userRepository;
        _eventService = eventService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
    }

    public async Task<IdentityResult> SetInitialMasterPasswordAsync(User user, string masterPassword, string key,
        string orgSsoIdentifier)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

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

        var org = await _organizationRepository.GetByIdentifierAsync(orgSsoIdentifier);

        if (org == null)
        {
            throw new BadRequestException("Organization invalid.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);

        if (orgUser == null)
        {
            throw new BadRequestException("User not found within organization.");
        }

        // TDE users who go from a user without admin acct recovery permission to having it will be
        // required to set a MP for the first time and we don't want to re-execute the accept logic
        // as they are already confirmed.
        // TLDR: only accept post SSO user if they are invited
        if (orgUser.Status == OrganizationUserStatusType.Invited)
        {
            await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
        }

        return IdentityResult.Success;
    }

}
