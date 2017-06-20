using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using System.Security.Claims;
using Bit.Core.Enums;
using Bit.Core.Models;

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
        Task SendTwoFactorEmailAsync(User user, string email = null);
        Task<bool> VerifyTwoFactorEmailAsync(User user, string token, string email = null);
        Task InitiateEmailChangeAsync(User user, string newEmail);
        Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword,
            string token, string key);
        Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword, string key);
        Task<IdentityResult> UpdateKeyAsync(User user, string masterPassword, string key, string privateKey,
            IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
        Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
        Task SetupTwoFactorAsync(User user, TwoFactorProviderType provider);
        Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type);
        Task DisableTwoFactorProviderAsync(User user, TwoFactorProviderType type);
        Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode);
        Task<string> GenerateUserTokenAsync(User user, string tokenProvider, string purpose);
        Task<IdentityResult> DeleteAsync(User user);
    }
}
