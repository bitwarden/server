using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Domains;
using System.Security.Claims;

namespace Bit.Core.Services
{
    public interface IUserService
    {
        Guid? GetProperUserId(ClaimsPrincipal principal);
        Task<User> GetUserByIdAsync(string userId);
        Task<User> GetUserByIdAsync(Guid userId);
        Task<User> GetUserByPrincipalAsync(ClaimsPrincipal principal);
        Task<DateTime> GetAccountRevisionDateByIdAsync(Guid userId);
        Task SaveUserAsync(User user);
        Task<IdentityResult> RegisterUserAsync(User user, string masterPassword);
        Task SendMasterPasswordHintAsync(string email);
        Task InitiateEmailChangeAsync(User user, string newEmail);
        Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword, string token, IEnumerable<Cipher> ciphers);
        Task<IdentityResult> ChangePasswordAsync(User user, string currentMasterPasswordHash, string newMasterPasswordHash, IEnumerable<Cipher> ciphers);
        Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
        Task GetTwoFactorAsync(User user, Enums.TwoFactorProviderType provider);
        Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode);
        Task<IdentityResult> DeleteAsync(User user);
    }
}
