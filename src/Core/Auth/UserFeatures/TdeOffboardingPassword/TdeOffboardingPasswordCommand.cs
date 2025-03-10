using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class TdeOffboardingPasswordCommand : ITdeOffboardingPasswordCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ISsoUserRepository _ssoUserRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IPushNotificationService _pushService;


    public TdeOffboardingPasswordCommand(
        IUserService userService,
        IUserRepository userRepository,
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        ISsoUserRepository ssoUserRepository,
        ISsoConfigRepository ssoConfigRepository,
        IPushNotificationService pushService)
    {
        _userService = userService;
        _userRepository = userRepository;
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _ssoUserRepository = ssoUserRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _pushService = pushService;
    }

    public async Task<IdentityResult> UpdateTdeOffboardingPasswordAsync(User user, string newMasterPassword, string key, string hint)
    {
        if (string.IsNullOrWhiteSpace(newMasterPassword))
        {
            throw new BadRequestException("Master password is required.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BadRequestException("Key is required.");
        }

        if (user.HasMasterPassword())
        {
            throw new BadRequestException("User already has a master password.");
        }
        var orgUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id);
        orgUserDetails = orgUserDetails.Where(x => x.UseSso).ToList();
        if (orgUserDetails.Count == 0)
        {
            throw new BadRequestException("User is not part of any organization that has SSO enabled.");
        }

        var orgSSOUsers = await Task.WhenAll(orgUserDetails.Select(async x => await _ssoUserRepository.GetByUserIdOrganizationIdAsync(x.OrganizationId, user.Id)));
        if (orgSSOUsers.Length != 1)
        {
            throw new BadRequestException("User is part of no or multiple SSO configurations.");
        }

        var orgUser = orgUserDetails.First();
        var orgSSOConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(orgUser.OrganizationId);
        if (orgSSOConfig == null)
        {
            throw new BadRequestException("Organization SSO configuration not found.");
        }
        else if (orgSSOConfig.GetData().MemberDecryptionType != Enums.MemberDecryptionType.MasterPassword)
        {
            throw new BadRequestException("Organization SSO Member Decryption Type is not Master Password.");
        }

        var result = await _userService.UpdatePasswordHash(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.ForcePasswordReset = false;
        user.Key = key;
        user.MasterPasswordHint = hint;

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_UpdatedTempPassword);
        await _pushService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }

}
