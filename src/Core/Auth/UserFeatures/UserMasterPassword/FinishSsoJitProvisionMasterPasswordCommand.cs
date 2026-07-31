using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class FinishSsoJitProvisionMasterPasswordCommand : IFinishSsoJitProvisionMasterPasswordCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IEventService _eventService;

    public FinishSsoJitProvisionMasterPasswordCommand(
        IUserService userService,
        IUserRepository userRepository,
        IMasterPasswordService masterPasswordService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IEventService eventService)
    {
        _userService = userService;
        _userRepository = userRepository;
        _masterPasswordService = masterPasswordService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _eventService = eventService;
    }

    public async Task FinishProvisionAsync(User user,
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

        var updateUserDataTasks = new List<UpdateUserData>
        {
            _masterPasswordService.BuildUpdateUserDelegateSetInitialMasterPassword(
                user,
                masterPasswordDataModel.ToSetInitialPasswordData())
        };

        // Unlike the other set-password flows, this one provisions the user key rather than
        // re-wrapping an existing one — the user.Key guard above establishes that the account has no
        // key material yet — so the supplied key id is authoritative and written outright.
        // A client that predates the key id field sends none. The account then picks one up from the
        // backfill endpoint on a later sync rather than at provisioning.
        var userKeyId = masterPasswordDataModel.MasterPasswordUnlock.ContainedKeyId();
        if (userKeyId != null)
        {
            updateUserDataTasks.Add(_userRepository.SetUserKeyId(user.Id, userKeyId));
        }

        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, masterPasswordDataModel.AccountKeys,
            updateUserDataTasks);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
    }
}
