using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopMailService : IMailService
    {
        public Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            return Task.FromResult(0);
        }

        public Task SendVerifyEmailEmailAsync(string email, Guid userId, string hint)
        {
            return Task.FromResult(0);
        }

        public Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            return Task.FromResult(0);
        }

        public Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            return Task.FromResult(0);
        }

        public Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail, IEnumerable<string> adminEmails)
        {
            return Task.FromResult(0);
        }

        public Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            return Task.FromResult(0);
        }

        public Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendTwoFactorEmailAsync(string email, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendWelcomeEmailAsync(User user)
        {
            return Task.FromResult(0);
        }

        public Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendPasswordlessSignInAsync(string returnUrl, string token, string email)
        {
            return Task.FromResult(0);
        }

        public Task SendInvoiceUpcomingAsync(string email, decimal amount, DateTime dueDate,
            List<string> items, bool mentionInvoices)
        {
            return Task.FromResult(0);
        }

        public Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices)
        {
            return Task.FromResult(0);
        }

        public Task SendAddedCreditAsync(string email, decimal amount)
        {
            return Task.FromResult(0);
        }

        public Task SendNewDeviceLoggedInEmail(string email, string deviceType, DateTime timestamp, string ip)
        {
            return Task.FromResult(0);
        }
    }
}
