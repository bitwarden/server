// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;

namespace Bit.Core.Auth.Services;

public class EmergencyAccessService : IEmergencyAccessService
{
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ICipherService _cipherService;
    private readonly IMailService _mailService;
    private readonly IUserService _userService;
    private readonly GlobalSettings _globalSettings;
    private readonly IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> _dataProtectorTokenizer;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;

    public EmergencyAccessService(
        IEmergencyAccessRepository emergencyAccessRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IPolicyRepository policyRepository,
        ICipherService cipherService,
        IMailService mailService,
        IUserService userService,
        GlobalSettings globalSettings,
        IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> dataProtectorTokenizer,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand)
    {
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _policyRepository = policyRepository;
        _cipherService = cipherService;
        _mailService = mailService;
        _userService = userService;
        _globalSettings = globalSettings;
        _dataProtectorTokenizer = dataProtectorTokenizer;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
    }

    public async Task<EmergencyAccess> InviteAsync(User grantorUser, string emergencyContactEmail, EmergencyAccessType accessType, int waitTime)
    {
        if (!await _userService.CanAccessPremium(grantorUser))
        {
            throw new BadRequestException("Not a premium user.");
        }

        if (accessType == EmergencyAccessType.Takeover && grantorUser.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot use Emergency Access Takeover because you are using Key Connector.");
        }

        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            Email = emergencyContactEmail.ToLowerInvariant(),
            Status = EmergencyAccessStatusType.Invited,
            Type = accessType,
            WaitTimeDays = waitTime,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

        await _emergencyAccessRepository.CreateAsync(emergencyAccess);
        await SendInviteAsync(emergencyAccess, NameOrEmail(grantorUser));

        return emergencyAccess;
    }

    public async Task<EmergencyAccessDetails> GetAsync(Guid emergencyAccessId, Guid grantorId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetDetailsByIdGrantorIdAsync(emergencyAccessId, grantorId);
        if (emergencyAccess == null)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        return emergencyAccess;
    }

    public async Task ResendInviteAsync(User grantorUser, Guid emergencyAccessId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null || emergencyAccess.GrantorId != grantorUser.Id ||
            emergencyAccess.Status != EmergencyAccessStatusType.Invited)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await SendInviteAsync(emergencyAccess, NameOrEmail(grantorUser));
    }

    public async Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User granteeUser, string token, IUserService userService)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        if (!_dataProtectorTokenizer.TryUnprotect(token, out var data))
        {
            throw new BadRequestException("Invalid token.");
        }

        if (!data.IsValid(emergencyAccessId, granteeUser.Email))
        {
            throw new BadRequestException("Invalid token.");
        }

        if (emergencyAccess.Status == EmergencyAccessStatusType.Accepted)
        {
            throw new BadRequestException("Invitation already accepted. You will receive an email when the grantor confirms you as an emergency access contact.");
        }
        else if (emergencyAccess.Status != EmergencyAccessStatusType.Invited)
        {
            throw new BadRequestException("Invitation already accepted.");
        }

        // TODO PM-21687
        // Might not be reachable since the Tokenable.IsValid() does an email comparison
        if (string.IsNullOrWhiteSpace(emergencyAccess.Email) ||
            !emergencyAccess.Email.Equals(granteeUser.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("User email does not match invite.");
        }

        var granteeEmail = emergencyAccess.Email;

        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Email = null;

        var grantor = await userService.GetUserByIdAsync(emergencyAccess.GrantorId);

        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);
        await _mailService.SendEmergencyAccessAcceptedEmailAsync(granteeEmail, grantor.Email);

        return emergencyAccess;
    }

    public async Task DeleteAsync(Guid emergencyAccessId, Guid grantorId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        // TODO PM-19438/PM-21687
        // Not sure why the GrantorId and the GranteeId are supposed to be the same?
        if (emergencyAccess == null || (emergencyAccess.GrantorId != grantorId && emergencyAccess.GranteeId != grantorId))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await _emergencyAccessRepository.DeleteAsync(emergencyAccess);
    }

    public async Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAccessId, string key, Guid grantorId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null || emergencyAccess.Status != EmergencyAccessStatusType.Accepted ||
            emergencyAccess.GrantorId != grantorId)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(grantorId);
        if (emergencyAccess.Type == EmergencyAccessType.Takeover && grantor.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot use Emergency Access Takeover because you are using Key Connector.");
        }

        var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);

        emergencyAccess.Status = EmergencyAccessStatusType.Confirmed;
        emergencyAccess.KeyEncrypted = key;
        emergencyAccess.Email = null;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);
        await _mailService.SendEmergencyAccessConfirmedEmailAsync(NameOrEmail(grantor), grantee.Email);

        return emergencyAccess;
    }

    public async Task SaveAsync(EmergencyAccess emergencyAccess, User grantorUser)
    {
        if (!await _userService.CanAccessPremium(grantorUser))
        {
            throw new BadRequestException("Not a premium user.");
        }

        if (emergencyAccess.GrantorId != grantorUser.Id)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        if (emergencyAccess.Type == EmergencyAccessType.Takeover)
        {
            var grantor = await _userService.GetUserByIdAsync(emergencyAccess.GrantorId);
            if (grantor.UsesKeyConnector)
            {
                throw new BadRequestException("You cannot use Emergency Access Takeover because you are using Key Connector.");
            }
        }

        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);
    }

    // TODO PM-21687: rename this to something like InitiateRecoveryAsync, and something similar for Approve and Reject
    public async Task InitiateAsync(Guid emergencyAccessId, User granteeUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null || emergencyAccess.GranteeId != granteeUser.Id ||
            emergencyAccess.Status != EmergencyAccessStatusType.Confirmed)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        if (emergencyAccess.Type == EmergencyAccessType.Takeover && grantor.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot takeover an account that is using Key Connector.");
        }

        var now = DateTime.UtcNow;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        emergencyAccess.RevisionDate = now;
        emergencyAccess.RecoveryInitiatedDate = now;
        emergencyAccess.LastNotificationDate = now;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        await _mailService.SendEmergencyAccessRecoveryInitiated(emergencyAccess, NameOrEmail(granteeUser), grantor.Email);
    }

    public async Task ApproveAsync(Guid emergencyAccessId, User grantorUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (emergencyAccess == null || emergencyAccess.GrantorId != grantorUser.Id ||
            emergencyAccess.Status != EmergencyAccessStatusType.RecoveryInitiated)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);
        await _mailService.SendEmergencyAccessRecoveryApproved(emergencyAccess, NameOrEmail(grantorUser), grantee.Email);
    }

    public async Task RejectAsync(Guid emergencyAccessId, User grantorUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (emergencyAccess == null || emergencyAccess.GrantorId != grantorUser.Id ||
            (emergencyAccess.Status != EmergencyAccessStatusType.RecoveryInitiated &&
             emergencyAccess.Status != EmergencyAccessStatusType.RecoveryApproved))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        emergencyAccess.Status = EmergencyAccessStatusType.Confirmed;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);
        await _mailService.SendEmergencyAccessRecoveryRejected(emergencyAccess, NameOrEmail(grantorUser), grantee.Email);
    }

    public async Task<ICollection<Policy>> GetPoliciesAsync(Guid emergencyAccessId, User granteeUser)
    {
        // TODO PM-21687
        // Should we look up policies here or just verify the EmergencyAccess is correct
        // and handle policy logic else where? Should this be a query/Command?
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (!IsValidRequest(emergencyAccess, granteeUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        var grantorOrganizations = await _organizationUserRepository.GetManyByUserAsync(grantor.Id);
        var isOrganizationOwner = grantorOrganizations
            .Any(organization => organization.Type == OrganizationUserType.Owner);

        var policies = isOrganizationOwner ? await _policyRepository.GetManyByUserIdAsync(grantor.Id) : null;

        return policies;
    }

    // TODO PM-21687: rename this to something like InitiateRecoveryTakeoverAsync
    public async Task<(EmergencyAccess, User)> TakeoverAsync(Guid emergencyAccessId, User granteeUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (!IsValidRequest(emergencyAccess, granteeUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);
        // TODO PM-21687
        // Redundant check of the EmergencyAccessType -> checked in IsValidRequest() ln 308
        if (emergencyAccess.Type == EmergencyAccessType.Takeover && grantor.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot takeover an account that is using Key Connector.");
        }

        return (emergencyAccess, grantor);
    }

    // TODO PM-21687: rename this to something like FinishRecoveryTakeoverAsync
    public async Task PasswordAsync(Guid emergencyAccessId, User granteeUser, string newMasterPasswordHash, string key)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (!IsValidRequest(emergencyAccess, granteeUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        await _userService.UpdatePasswordHash(grantor, newMasterPasswordHash);
        grantor.RevisionDate = DateTime.UtcNow;
        grantor.LastPasswordChangeDate = grantor.RevisionDate;
        grantor.Key = key;
        // Disable TwoFactor providers since they will otherwise block logins
        grantor.SetTwoFactorProviders([]);
        // Disable New Device Verification since it will otherwise block logins
        grantor.VerifyDevices = false;
        await _userRepository.ReplaceAsync(grantor);

        // Remove grantor from all organizations unless Owner
        var orgUser = await _organizationUserRepository.GetManyByUserAsync(grantor.Id);
        foreach (var o in orgUser)
        {
            if (o.Type != OrganizationUserType.Owner)
            {
                await _removeOrganizationUserCommand.RemoveUserAsync(o.OrganizationId, grantor.Id);
            }
        }
    }

    public async Task SendNotificationsAsync()
    {
        var toNotify = await _emergencyAccessRepository.GetManyToNotifyAsync();

        foreach (var notify in toNotify)
        {
            var ea = notify.ToEmergencyAccess();
            ea.LastNotificationDate = DateTime.UtcNow;
            await _emergencyAccessRepository.ReplaceAsync(ea);

            var granteeNameOrEmail = string.IsNullOrWhiteSpace(notify.GranteeName) ? notify.GranteeEmail : notify.GranteeName;

            await _mailService.SendEmergencyAccessRecoveryReminder(ea, granteeNameOrEmail, notify.GrantorEmail);
        }
    }

    public async Task HandleTimedOutRequestsAsync()
    {
        var expired = await _emergencyAccessRepository.GetExpiredRecoveriesAsync();

        foreach (var details in expired)
        {
            var ea = details.ToEmergencyAccess();
            ea.Status = EmergencyAccessStatusType.RecoveryApproved;
            await _emergencyAccessRepository.ReplaceAsync(ea);

            var grantorNameOrEmail = string.IsNullOrWhiteSpace(details.GrantorName) ? details.GrantorEmail : details.GrantorName;
            var granteeNameOrEmail = string.IsNullOrWhiteSpace(details.GranteeName) ? details.GranteeEmail : details.GranteeName;

            await _mailService.SendEmergencyAccessRecoveryApproved(ea, grantorNameOrEmail, details.GranteeEmail);
            await _mailService.SendEmergencyAccessRecoveryTimedOut(ea, granteeNameOrEmail, details.GrantorEmail);
        }
    }

    public async Task<EmergencyAccessViewData> ViewAsync(Guid emergencyAccessId, User granteeUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (!IsValidRequest(emergencyAccess, granteeUser, EmergencyAccessType.View))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(emergencyAccess.GrantorId, withOrganizations: false);

        return new EmergencyAccessViewData
        {
            EmergencyAccess = emergencyAccess,
            Ciphers = ciphers,
        };
    }

    public async Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid emergencyAccessId, Guid cipherId, string attachmentId, User granteeUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        if (!IsValidRequest(emergencyAccess, granteeUser, EmergencyAccessType.View))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var cipher = await _cipherRepository.GetByIdAsync(cipherId, emergencyAccess.GrantorId);
        return await _cipherService.GetAttachmentDownloadDataAsync(cipher, attachmentId);
    }

    private async Task SendInviteAsync(EmergencyAccess emergencyAccess, string invitingUsersName)
    {
        var token = _dataProtectorTokenizer.Protect(new EmergencyAccessInviteTokenable(emergencyAccess, _globalSettings.OrganizationInviteExpirationHours));
        await _mailService.SendEmergencyAccessInviteEmailAsync(emergencyAccess, invitingUsersName, token);
    }

    // TODO PM-21687: move this to the user entity -> User.GetNameOrEmail()?
    private static string NameOrEmail(User user)
    {
        return string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name;
    }

    /*
     * Checks if EmergencyAccess Object is null
     * Checks the requesting user is the same as the granteeUser (So we are checking for proper grantee action)
     * Status _must_ equal RecoveryApproved (This means the grantor has invited, the grantee has accepted, and the grantor has approved so the shared key exists but hasn't been exercised yet)
     * request type must equal the type of access requested (View or Takeover)
     */
    //TODO PM-21687: this IsValidRequest() checks the validity based on the granteeUser. There should be a complementary method for the grantorUser
    private static bool IsValidRequest(
        EmergencyAccess availableAccess,
        User requestingUser,
        EmergencyAccessType requestedAccessType)
    {
        return availableAccess != null &&
           availableAccess.GranteeId == requestingUser.Id &&
           availableAccess.Status == EmergencyAccessStatusType.RecoveryApproved &&
           availableAccess.Type == requestedAccessType;
    }
}
