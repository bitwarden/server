using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(Guid userId);
        Task SaveUserAsync(User user);
        Task<IdentityResult> RegisterUserAsync(User user, string masterPassword);
        Task SendMasterPasswordHintAsync(string email);
        Task InitiateEmailChangeAsync(User user, string newEmail);
        Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword, string token, IEnumerable<Cipher> ciphers);
        Task<IdentityResult> ChangePasswordAsync(User user, string currentMasterPasswordHash, string newMasterPasswordHash, IEnumerable<Cipher> ciphers);
        Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
        Task GetTwoFactorAsync(User user, Enums.TwoFactorProvider provider);
        Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode);
        Task<IdentityResult> DeleteAsync(User user);
    }
}
