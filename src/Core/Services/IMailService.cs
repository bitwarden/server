using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;

namespace Bit.Core.Services
{
    public interface IMailService
    {
        Task SendWelcomeEmailAsync(User user);
        Task SendVerifyEmailEmailAsync(string email, Guid userId, string token);
        Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
        Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
        Task SendTwoFactorEmailAsync(string email, string token);
        Task SendNoMasterPasswordHintEmailAsync(string email);
        Task SendMasterPasswordHintEmailAsync(string email, string hint);
        Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token);
        Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail, IEnumerable<string> adminEmails);
        Task SendOrganizationConfirmedEmailAsync(string organizationName, string email);
    }
}