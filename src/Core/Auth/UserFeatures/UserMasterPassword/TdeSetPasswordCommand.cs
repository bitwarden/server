using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class TdeSetPasswordCommand : ITdeSetPasswordCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEventService _eventService;

    public TdeSetPasswordCommand(IUserRepository userRepository,
        IMasterPasswordService masterPasswordService,
        IOrganizationUserRepository organizationUserRepository, IOrganizationRepository organizationRepository,
        IPasswordHasher<User> passwordHasher, IEventService eventService)
    {
        _userRepository = userRepository;
        _masterPasswordService = masterPasswordService;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _eventService = eventService;
    }

    public async Task SetMasterPasswordAsync(User user, SetInitialMasterPasswordDataModel masterPasswordDataModel)
    {
        // TDE scenario specific check
        if (user.PublicKey == null || user.PrivateKey == null)
        {
            throw new BadRequestException("TDE user account keys must be set before setting initial master password.");
        }

        // Does this need to be here? Why is this here?
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

        var setMasterPasswordTask = _masterPasswordService.BuildTransactionForSetInitialMasterPasswordAsync(user,
            new SetInitialPasswordData
            {
                MasterPasswordUnlock = masterPasswordDataModel.MasterPasswordUnlock,
                MasterPasswordAuthentication = masterPasswordDataModel.MasterPasswordAuthentication,
                MasterPasswordHint = masterPasswordDataModel.MasterPasswordHint,
            });

        await _userRepository.UpdateUserDataAsync([setMasterPasswordTask]);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
    }
}
