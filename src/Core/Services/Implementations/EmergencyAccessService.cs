using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Models.Api.Response;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Services
{
    public class EmergencyAccessService : IEmergencyAccessService
    {
        private readonly IEmergencyAccessRepository _emergencyAccessRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IMailService _mailService;
        private readonly IUserService _userService;
        private readonly IDataProtector _dataProtector;
        private readonly GlobalSettings _globalSettings;
        private readonly IPasswordHasher<User> _passwordHasher;

        public EmergencyAccessService(
            IEmergencyAccessRepository emergencyAccessRepository,
            IUserRepository userRepository,
            ICipherRepository cipherRepository,
            IMailService mailService,
            IUserService userService,
            IPasswordHasher<User> passwordHasher,
            IDataProtectionProvider dataProtectionProvider,
            GlobalSettings globalSettings)
        {
            _emergencyAccessRepository = emergencyAccessRepository;
            _userRepository = userRepository;
            _cipherRepository = cipherRepository;
            _mailService = mailService;
            _userService = userService;
            _passwordHasher = passwordHasher;
            _dataProtector = dataProtectionProvider.CreateProtector("EmergencyAccessServiceDataProtector");
            _globalSettings = globalSettings;
        }

        public async Task<EmergencyAccess> InviteAsync(User invitingUser, string invitingUsersName, string email, EmergencyAccessType type, int waitTime)
        {
            if (! await _userService.CanAccessPremium(invitingUser))
            {
                throw new BadRequestException("Not a premium user.");
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
            await SendInviteAsync(emergencyAccess, invitingUsersName);

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

        public async Task ResendInviteAsync(Guid invitingUserId, Guid emergencyAccessId, string invitingUsersName)
        {
            var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
            if (emergencyAccess == null || emergencyAccess.GrantorId != invitingUserId ||
                emergencyAccess.Status != EmergencyAccessStatusType.Invited)
            {
                throw new BadRequestException("Emergency Access not valid.");
            }

            await SendInviteAsync(emergencyAccess, invitingUsersName);
        }

        public async Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User user, string token, IUserService userService)
        {
            var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);
            if (emergencyAccess == null)
            {
                throw new BadRequestException("Emergency Access not valid.");
            }

            if (!CoreHelpers.TokenIsValid("EmergencyAccessInvite", _dataProtector, token, user.Email, emergencyAccessId, _globalSettings))
            {
                throw new BadRequestException("Invalid token.");
            }

            if (string.IsNullOrWhiteSpace(emergencyAccess.Email) ||
                !emergencyAccess.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new BadRequestException("User email does not match invite.");
            }

            if (emergencyAccess.Status != EmergencyAccessStatusType.Invited)
            {
                throw new BadRequestException("Already accepted.");
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
            var grantee = await _userRepository.GetByIdAsync(emergencyAccess.GranteeId.Value);

            emergencyAccess.Status = EmergencyAccessStatusType.Confirmed;
            emergencyAccess.KeyEncrypted = key;
            emergencyAccess.Email = null;
            await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);
            await _mailService.SendEmergencyAccessConfirmedEmailAsync(grantor.Name, grantee.Email);

            return emergencyAccess;
        }

        public async Task SaveAsync(EmergencyAccess emergencyAccess, Guid savingUserId)
        {
            if (emergencyAccess.GrantorId != savingUserId)
            {
                throw new BadRequestException("Emergency Access not valid.");
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

            var now = DateTime.UtcNow;
            emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
            emergencyAccess.RevisionDate = now;
            emergencyAccess.RecoveryInitiatedDate = now;
            emergencyAccess.LastNotificationDate = now;
            await _emergencyAccessRepository.ReplaceAsync(emergencyAccess);

            var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

            await _mailService.SendEmergencyAccessRecoveryInitiated(emergencyAccess, initiatingUser.Name, grantor.Email);
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
            await _mailService.SendEmergencyAccessRecoveryApproved(emergencyAccess, approvingUser.Name, grantee.Email);
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
            await _mailService.SendEmergencyAccessRecoveryRejected(emergencyAccess, rejectingUser.Name, grantee.Email);
        }

        public async Task<(EmergencyAccess, User)> TakeoverAsync(Guid id, User requestingUser)
        {
            var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

            if (emergencyAccess == null || emergencyAccess.GranteeId != requestingUser.Id ||
                emergencyAccess.Status != EmergencyAccessStatusType.RecoveryApproved)
            {
                throw new BadRequestException("Emergency Access not valid.");
            }

            var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);
            
            return (emergencyAccess, grantor);
        }

        public async Task PasswordAsync(Guid id, User requestingUser, string newMasterPasswordHash, string key)
        {
            var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

            if (emergencyAccess == null || emergencyAccess.GranteeId != requestingUser.Id ||
                emergencyAccess.Status != EmergencyAccessStatusType.RecoveryApproved)
            {
                throw new BadRequestException("Emergency Access not valid.");
            }

            var grantor = await _userRepository.GetByIdAsync(emergencyAccess.GrantorId);

            grantor.MasterPassword = _passwordHasher.HashPassword(grantor, newMasterPasswordHash);
            grantor.Key = key;
            // Disable TwoFactor providers since they will otherwise block logins
            grantor.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>());
            await _userRepository.ReplaceAsync(grantor);
        }

        public async Task SendNotificationsAsync()
        {
            var toNotify = await _emergencyAccessRepository.GetManyToNotifyAsync();

            foreach (var notify in toNotify)
            {
                var ea = notify.ToEmergencyAccess();
                ea.LastNotificationDate = DateTime.UtcNow;
                await _emergencyAccessRepository.ReplaceAsync(ea);
                
                await _mailService.SendEmergencyAccessRecoveryReminder(ea, notify.GranteeName, notify.GrantorEmail);
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
                
                await _mailService.SendEmergencyAccessRecoveryApproved(ea, details.GrantorName, details.GranteeEmail);
                await _mailService.SendEmergencyAccessRecoveryTimedOut(ea, details.GranteeName, details.GrantorEmail);
            }
        }

        public async Task<EmergencyAccessViewResponseModel> ViewAsync(Guid id, User requestingUser)
        {
            var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(id);

            if (emergencyAccess == null || emergencyAccess.GranteeId != requestingUser.Id ||
                emergencyAccess.Status != EmergencyAccessStatusType.RecoveryApproved)
            {
                throw new BadRequestException("Emergency Access not valid.");
            }
            
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(emergencyAccess.GrantorId, false);
            
            return new EmergencyAccessViewResponseModel(_globalSettings, emergencyAccess, ciphers);
        }

        private async Task SendInviteAsync(EmergencyAccess emergencyAccess, string invitingUsersName)
        {
            var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
            var token = _dataProtector.Protect($"EmergencyAccessInvite {emergencyAccess.Id} {emergencyAccess.Email} {nowMillis}");
            await _mailService.SendEmergencyAccessInviteEmailAsync(emergencyAccess, invitingUsersName, token);
        }
    }
}
