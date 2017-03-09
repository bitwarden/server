using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IMailService
    {
        Task SendWelcomeEmailAsync(User user);
        Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
        Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
        Task SendNoMasterPasswordHintEmailAsync(string email);
        Task SendMasterPasswordHintEmailAsync(string email, string hint);
    }
}