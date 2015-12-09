using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(string userId);
        Task SaveUserAsync(User user);
        Task InitiateRegistrationAsync(string email);
        Task<IdentityResult> RegisterUserAsync(string token, User user, string masterPassword);
        Task SendMasterPasswordHintAsync(string email);
        Task InitiateEmailChangeAsync(User user, string newEmail);
        Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword, string token, IEnumerable<dynamic> ciphers);
        Task<IdentityResult> ChangePasswordAsync(User user, string currentMasterPasswordHash, string newMasterPasswordHash, IEnumerable<dynamic> ciphers);
        Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
        Task GetTwoFactorAsync(User user, Enums.TwoFactorProvider provider);
    }
}
