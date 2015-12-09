using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface IMailService
    {
        Task SendAlreadyRegisteredEmailAsync(string registrantEmailAddress);
        Task SendRegisterEmailAsync(string registrantEmailAddress, string token);
        Task SendWelcomeEmailAsync(User user);
        Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
        Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
        Task SendNoMasterPasswordHintEmailAsync(string email);
        Task SendMasterPasswordHintEmailAsync(string email, string hint);
    }
}