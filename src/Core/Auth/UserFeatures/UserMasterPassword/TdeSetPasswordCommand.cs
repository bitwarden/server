using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class TdeSetPasswordCommand : ITdeSetPasswordCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMasterPasswordHasher _masterPasswordHasher;
    private readonly IEventService _eventService;

    public TdeSetPasswordCommand(IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository, IOrganizationRepository organizationRepository,
        IMasterPasswordHasher masterPasswordHasher, IEventService eventService)
    {
        _userRepository = userRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _masterPasswordHasher = masterPasswordHasher;
        _eventService = eventService;
    }

    public async Task SetMasterPasswordAsync(User user, SetInitialMasterPasswordDataModel masterPasswordDataModel)
    {
        if (user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        if (user.PublicKey == null || user.PrivateKey == null)
        {
            throw new BadRequestException("TDE user account keys must be set before setting initial master password.");
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

        // Hash the provided user master password authentication hash on the server side
        var serverSideHashedMasterPasswordAuthenticationHash = _masterPasswordHasher.HashPassword(user,
            masterPasswordDataModel.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);

        var setMasterPasswordTask = _userRepository.SetMasterPassword(user.Id,
            masterPasswordDataModel.MasterPasswordUnlock, serverSideHashedMasterPasswordAuthenticationHash,
            masterPasswordDataModel.MasterPasswordHint);
        await _userRepository.UpdateUserDataAsync([setMasterPasswordTask]);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
    }
}
