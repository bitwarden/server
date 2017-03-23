using System;
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

        public Task SendOrganizationInviteEmailAsync(string organizationName, string email, string token)
        {
            return Task.FromResult(0);
        }

        public Task SendWelcomeEmailAsync(User user)
        {
            return Task.FromResult(0);
        }
    }
}
