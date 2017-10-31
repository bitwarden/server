using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class BackupMailService : IMailService
    {
        private readonly IMailService _primaryMailService;
        private readonly IMailService _backupMailService;
        private readonly ILogger<BackupMailService> _logger;

        public BackupMailService(
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService,
            ILogger<BackupMailService> logger)
        {
            _primaryMailService = new RazorMailService(globalSettings, mailDeliveryService);
            _backupMailService = new MarkdownMailService(globalSettings, mailDeliveryService);
            _logger = logger;
        }

        public async Task SendVerifyEmailEmailAsync(string email, Guid userId, string token)
        {
            try
            {
                await _primaryMailService.SendVerifyEmailEmailAsync(email, userId, token);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendVerifyEmailEmailAsync(email, userId, token);
            }
        }

        public async Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
        {
            try
            {
                await _primaryMailService.SendVerifyDeleteEmailAsync(email, userId, token);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendVerifyDeleteEmailAsync(email, userId, token);
            }
        }

        public async Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            try
            {
                await _primaryMailService.SendChangeEmailAlreadyExistsEmailAsync(fromEmail, toEmail);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendChangeEmailAlreadyExistsEmailAsync(fromEmail, toEmail);
            }
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            try
            {
                await _primaryMailService.SendChangeEmailEmailAsync(newEmailAddress, token);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendChangeEmailEmailAsync(newEmailAddress, token);
            }
        }

        public async Task SendTwoFactorEmailAsync(string email, string token)
        {
            try
            {
                await _primaryMailService.SendTwoFactorEmailAsync(email, token);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendTwoFactorEmailAsync(email, token);
            }
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            try
            {
                await _primaryMailService.SendMasterPasswordHintEmailAsync(email, hint);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendMasterPasswordHintEmailAsync(email, hint);
            }
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            try
            {
                await _primaryMailService.SendNoMasterPasswordHintEmailAsync(email);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendNoMasterPasswordHintEmailAsync(email);
            }
        }

        public async Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail,
            IEnumerable<string> adminEmails)
        {
            try
            {
                await _primaryMailService.SendOrganizationAcceptedEmailAsync(organizationName, userEmail, adminEmails);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendOrganizationAcceptedEmailAsync(organizationName, userEmail, adminEmails);
            }
        }

        public async Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            try
            {
                await _primaryMailService.SendOrganizationConfirmedEmailAsync(organizationName, email);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendOrganizationConfirmedEmailAsync(organizationName, email);
            }
        }

        public async Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            try
            {
                await _primaryMailService.SendOrganizationInviteEmailAsync(organizationName, orgUser, token);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendOrganizationInviteEmailAsync(organizationName, orgUser, token);
            }
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            try
            {
                await _primaryMailService.SendWelcomeEmailAsync(user);
            }
            catch(Exception e)
            {
                LogError(e);
                await _backupMailService.SendWelcomeEmailAsync(user);
            }
        }

        private void LogError(Exception e)
        {
            _logger.LogError(e, "Error sending mail with primary service, using backup.");
        }
    }
}
