using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class FinishSsoJitProvisionMasterPasswordCommand(
    IUserRepository userRepository,
    IUserService userService,
    IMasterPasswordService masterPasswordService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IEventService eventService,
    IAcceptOrgUserCommand acceptOrgUserCommand) : IFinishSsoJitProvisionMasterPasswordCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserService _userService = userService;
    private readonly IMasterPasswordService _masterPasswordService = masterPasswordService;
    private readonly IOrganizationUserRepository _organizationUserRepository = organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IEventService _eventService = eventService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand = acceptOrgUserCommand;

    /// <inheritdoc/>
    public async Task FinishProvisionAsync(
        User user,
        SetInitialMasterPasswordDataModel masterPasswordDataModel,
        bool acceptOrganizationInvite = true)
    {
        if (user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        if (masterPasswordDataModel.AccountKeys == null)
        {
            throw new BadRequestException("Account keys are required.");
        }

        var org = await _organizationRepository.GetByIdentifierAsync(masterPasswordDataModel.OrgSsoIdentifier)
            ?? throw new BadRequestException("Organization SSO identifier is invalid.");

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id)
            ?? throw new BadRequestException("User not found within organization.");

        var updateUserDataTasks = new List<UpdateUserData>
        {
            _masterPasswordService.BuildUpdateUserDelegateSetInitialMasterPassword(
                user,
                masterPasswordDataModel.ToSetInitialPasswordData())
        };

        var containedKeyId = masterPasswordDataModel.MasterPasswordUnlock.ContainedKeyId;
        if (containedKeyId is not null)
        {
            updateUserDataTasks.Add(_userRepository.SetUserKeyId(user.Id, containedKeyId));
        }

        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, masterPasswordDataModel.AccountKeys,
            updateUserDataTasks);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        if (acceptOrganizationInvite)
        {
            await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
        }
    }
}
