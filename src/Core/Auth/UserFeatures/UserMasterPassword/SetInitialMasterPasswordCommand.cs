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

    public async Task SetInitialMasterPasswordAsync(User user,
        SetInitialMasterPasswordDataModel masterPasswordDataModel)
    {
        if (user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        if (masterPasswordDataModel.AccountKeys == null)
        {
            throw new BadRequestException("Account keys are required.");
        }

        // Prevent a de-synced salt value from creating an un-decryptable unlock method
        masterPasswordDataModel.MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
        masterPasswordDataModel.MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);

        var org = await _organizationRepository.GetByIdentifierAsync(masterPasswordDataModel.OrgSsoIdentifier);
        if (org == null)
        {
            throw new BadRequestException("Organization SSO identifier is invalid.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException("User not found within organization.");
        }

        // Hash the provided user master password hash on the server side
        var serverSideMasterPasswordHash = _passwordHasher.HashPassword(user,
            masterPasswordDataModel.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);

        var setMasterPasswordTask = _userRepository.SetMasterPassword(user.Id,
            masterPasswordDataModel.MasterPasswordUnlock, serverSideMasterPasswordHash,
            masterPasswordDataModel.MasterPasswordHint);
        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, masterPasswordDataModel.AccountKeys,
            [setMasterPasswordTask]);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
    }
}
