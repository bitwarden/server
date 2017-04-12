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
            throw new NotImplementedException();
        }

        public Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            throw new NotImplementedException();
        }

        public Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendWelcomeEmailAsync(User user)
        {
            return Task.FromResult(0);
        }
    }
}
