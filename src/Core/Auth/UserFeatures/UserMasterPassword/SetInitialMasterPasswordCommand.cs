using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class SetInitialMasterPasswordCommand : ISetInitialMasterPasswordCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEventService _eventService;

    public SetInitialMasterPasswordCommand(IUserService userService, IUserRepository userRepository,
        IAcceptOrgUserCommand acceptOrgUserCommand, IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository, IPasswordHasher<User> passwordHasher,
        IEventService eventService)
    {
        _userService = userService;
        _userRepository = userRepository;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _eventService = eventService;
    }

    public async Task SetInitialMasterPasswordAsync(User user, SetMasterPasswordDataModel setMasterPasswordData)
    {
        if (user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        // TDE users don't send account keys, since they already have account keys set
        if (setMasterPasswordData.AccountKeys == null)
        {
            if (user.PublicKey == null || user.PrivateKey == null)
            {
                throw new BadRequestException(
                    "TDE user account keys must be set before setting initial master password.");
            }
        }

        // Prevent a de-synced salt value from creating an un-decryptable unlock method
        setMasterPasswordData.MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
        setMasterPasswordData.MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);

        var org = await _organizationRepository.GetByIdentifierAsync(setMasterPasswordData.OrgSsoIdentifier);
        if (org == null)
        {
            throw new BadRequestException("Organization invalid.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException("User not found within organization.");
        }

        var result = await _userService.ValidatePasswordHashAsync(user,
            setMasterPasswordData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);
        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        var masterPasswordHash = _passwordHasher.HashPassword(user,
            setMasterPasswordData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);

        var setInitialMasterPasswordTask = _userRepository.SetInitialMasterPassword(user.Id,
            setMasterPasswordData.MasterPasswordUnlock, masterPasswordHash, setMasterPasswordData.MasterPasswordHint);
        if (setMasterPasswordData.AccountKeys != null)
        {
            // Master password users need to have account keys set
            await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, setMasterPasswordData.AccountKeys,
                [setInitialMasterPasswordTask]);
        }
        else
        {
            // TDE users already have account keys set
            await _userRepository.UpdateUserDataAsync([setInitialMasterPasswordTask]);
        }

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        // TDE users who go from a user without admin account recovery permission to having it will be
        // required to set a MP for the first time. We don't want to re-execute the accept logic
        // as they are already confirmed.
        // TLDR: only accept post SSO user if they are invited
        if (orgUser.Status == OrganizationUserStatusType.Invited)
        {
            await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
        }
    }
}
