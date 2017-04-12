using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public interface IMailService
    {
        Task SendWelcomeEmailAsync(User user);
        Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
        Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
        Task SendNoMasterPasswordHintEmailAsync(string email);
        Task SendMasterPasswordHintEmailAsync(string email, string hint);
        Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token);
        Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail, IEnumerable<string> adminEmails);
        Task SendOrganizationConfirmedEmailAsync(string organizationName, string email);
    }
}