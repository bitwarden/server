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
        Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token);
        Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
        Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
        Task SendTwoFactorEmailAsync(string email, string token);
        Task SendNoMasterPasswordHintEmailAsync(string email);
        Task SendMasterPasswordHintEmailAsync(string email, string hint);
        Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token);
        Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail,
            IEnumerable<string> adminEmails);
        Task SendOrganizationConfirmedEmailAsync(string organizationName, string email);
        Task SendPasswordlessSignInAsync(string returnUrl, string token, string email);
        Task SendInvoiceUpcomingAsync(string email, decimal amount, DateTime dueDate, List<string> items,
            bool mentionInvoices);
        Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices);
        Task SendAddedCreditAsync(string email, decimal amount);
        Task SendNewDeviceLoggedInEmail(string email, string deviceType, DateTime timestamp, string ip);
    }
}
