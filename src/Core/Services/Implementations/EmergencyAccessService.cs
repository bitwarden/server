using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Services;

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
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOrganizationService _organizationService;
    private readonly IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> _dataProtectorTokenizer;

    public EmergencyAccessService(
        IEmergencyAccessRepository emergencyAccessRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IPolicyRepository policyRepository,
        ICipherService cipherService,
        IMailService mailService,
        IUserService userService,
        IPasswordHasher<User> passwordHasher,
        GlobalSettings globalSettings,
        IOrganizationService organizationService,
        IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> dataProtectorTokenizer)
    {
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _policyRepository = policyRepository;
        _cipherService = cipherService;
        _mailService = mailService;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _globalSettings = globalSettings;
        _organizationService = organizationService;
        _dataProtectorTokenizer = dataProtectorTokenizer;
    }

    public async Task<EmergencyAccess> InviteAsync(User invitingUser, string email, EmergencyAccessType type, int waitTime)
    {
        if (!await _userService.CanAccessPremium(invitingUser))
        {
            throw new BadRequestException("Not a premium user.");
        }

        if (type == EmergencyAccessType.Takeover && invitingUser.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot use Emergency Access Takeover because you are using Key Connector.");
        }

        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = invitingUser.Id,
            Email = email.ToLowerInvariant(),
            Status = EmergencyAccessStatusType.Invited,
            Type = type,
            WaitTimeDays = waitTime,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

        await _emergencyAccessRepository.CreateAsync(emergencyAccess);
        await SendInviteAsync(emergencyAccess, NameOrEmail(invitingUser));

        return emergencyAccess;
    }

    public async Task<EmergencyAccessDetails> GetAsync(Guid emergencyAccessId, Guid userId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetDetailsByIdGrantorIdAsync(emergencyAccessId, userId);
        if (emergencyAccess == null)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        return emergencyAccess;
    }

    public async Task ResendInviteAsync(User invitingUser, Guid emergencyAccessId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null || emergencyAccess.GrantorId != invitingUser.Id ||
            emergencyAccess.Status != EmergencyAccessStatusType.Invited)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await SendInviteAsync(emergencyAccess, NameOrEmail(invitingUser));
    }

    public async Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User user, string token, IUserService userService)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        if (!_dataProtectorTokenizer.TryUnprotect(token, out var data) && data.IsValid(emergencyAccessId, user.Email))
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

        if (string.IsNullOrWhiteSpace(emergencyAccess.Email) ||
            !emergencyAccess.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("User email does not match invite.");
        }

        var granteeEmail = emergencyAccess.Email;

        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        emergencyAccess.GranteeId = user.Id;
        emergencyAccess.Email = null;

        var grantor = await userService.GetUserByIdAsync(emergencyAccess.GrantorId);

        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);
        await _mailService.SendEmergencyAccessAcceptedEmailAsync(granteeEmail, grantor.Email);

        return emergencyAccess;
    }

    public async Task DeleteAsync(Guid emergencyAccessId, Guid grantorId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
        if (emergencyAccess == null || (emergencyAccess.GrantorId != grantorId && emergencyAccess.GranteeId != grantorId))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await _emergencyAccessRepository.DeleteAsync(emergencyAccess);
    }

    public async Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAcccessId, string key, Guid confirmingUserId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAcccessId);
        if (emergencyAccess == null || emergencyAccess.Status != EmergencyAccessStatusType.Accepted ||
            emergencyAccess.GrantorId != confirmingUserId)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(confirmingUserId);
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

    public async Task SaveAsync(EmergencyAccess emergencyAccess, User savingUser)
    {
        if (!await _userService.CanAccessPremium(savingUser))
        {
            throw new BadRequestException("Not a premium user.");
        }

        if (emergencyAccess.GrantorId != savingUser.Id)
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

    public async Task InitiateAsync(Guid id, User initiatingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (emergencyAccess == null || emergencyAccess.GranteeId != initiatingUser.Id ||
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

        await _mailService.SendEmergencyAccessRecoveryInitiated(emergencyAccess, NameOrEmail(initiatingUser), grantor.Email);
    }

    public async Task ApproveAsync(Guid id, User approvingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (emergencyAccess == null || emergencyAccess.GrantorId != approvingUser.Id ||
            emergencyAccess.Status != EmergencyAccessStatusType.RecoveryInitiated)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);
        await _mailService.SendEmergencyAccessRecoveryApproved(emergencyAccess, NameOrEmail(approvingUser), grantee.Email);
    }

    public async Task RejectAsync(Guid id, User rejectingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (emergencyAccess == null || emergencyAccess.GrantorId != rejectingUser.Id ||
            (emergencyAccess.Status != EmergencyAccessStatusType.RecoveryInitiated &&
             emergencyAccess.Status != EmergencyAccessStatusType.RecoveryApproved))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        emergencyAccess.Status = EmergencyAccessStatusType.Confirmed;
        await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

        var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);
        await _mailService.SendEmergencyAccessRecoveryRejected(emergencyAccess, NameOrEmail(rejectingUser), grantee.Email);
    }

    public async Task<ICollection<Policy>> GetPoliciesAsync(Guid id, User requestingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (!IsValidRequest(emergencyAccess, requestingUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        var grantorOrganizations = await _organizationUserRepository.GetManyByUserAsync(grantor.Id);
        var isOrganizationOwner = grantorOrganizations.Any<OrganizationUser>(organization => organization.Type == OrganizationUserType.Owner);
        var policies = isOrganizationOwner ? await _policyRepository.GetManyByUserIdAsync(grantor.Id) : null;

        return policies;
    }

    public async Task<(EmergencyAccess, User)> TakeoverAsync(Guid id, User requestingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (!IsValidRequest(emergencyAccess, requestingUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        if (emergencyAccess.Type == EmergencyAccessType.Takeover && grantor.UsesKeyConnector)
        {
            throw new BadRequestException("You cannot takeover an account that is using Key Connector.");
        }

        return (emergencyAccess, grantor);
    }

    public async Task PasswordAsync(Guid id, User requestingUser, string newMasterPasswordHash, string key)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (!IsValidRequest(emergencyAccess, requestingUser, EmergencyAccessType.Takeover))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

        grantor.MasterPassword = _passwordHasher.HashPassword(grantor, newMasterPasswordHash);
        grantor.Key = key;
        // Disable TwoFactor providers since they will otherwise block logins
        grantor.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>());
        grantor.UnknownDeviceVerificationEnabled = false;
        await _userRepository.ReplaceAsync(grantor);

        // Remove grantor from all organizations unless Owner
        var orgUser = await _organizationUserRepository.GetManyByUserAsync(grantor.Id);
        foreach (var o in orgUser)
        {
            if (o.Type != OrganizationUserType.Owner)
            {
                await _organizationService.DeleteUserAsync(o.OrganizationId, grantor.Id);
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

    public async Task<EmergencyAccessViewData> ViewAsync(Guid id, User requestingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (!IsValidRequest(emergencyAccess, requestingUser, EmergencyAccessType.View))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(emergencyAccess.GrantorId, false);

        return new EmergencyAccessViewData
        {
            EmergencyAccess = emergencyAccess,
            Ciphers = ciphers,
        };
    }

    public async Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid id, Guid cipherId, string attachmentId, User requestingUser)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

        if (!IsValidRequest(emergencyAccess, requestingUser, EmergencyAccessType.View))
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

    private string NameOrEmail(User user)
    {
        return string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name;
    }

    private bool IsValidRequest(EmergencyAccess availibleAccess, User requestingUser, EmergencyAccessType requestedAccessType)
    {
        return availibleAccess != null &&
           availibleAccess.GranteeId == requestingUser.Id &&
           availibleAccess.Status == EmergencyAccessStatusType.RecoveryApproved &&
           availibleAccess.Type == requestedAccessType;
    }
}
