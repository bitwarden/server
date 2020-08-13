using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using System.Security.Claims;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Business;

namespace Bit.Core.Services
{
    public interface IUserService
    {
        Guid? GetProperUserId(ClaimsPrincipal principal);
        Task<User> GetUserByIdAsync(string userId);
        Task<User> GetUserByIdAsync(Guid userId);
        Task<User> GetUserByPrincipalAsync(ClaimsPrincipal principal);
        Task<DateTime> GetAccountRevisionDateByIdAsync(Guid userId);
        Task SaveUserAsync(User user, bool push = false);
        Task<IdentityResult> RegisterUserAsync(User user, string masterPassword, string token, Guid? orgUserId);
        Task<IdentityResult> RegisterUserAsync(User user);
        Task SendMasterPasswordHintAsync(string email);
        Task SendTwoFactorEmailAsync(User user);
        Task<bool> VerifyTwoFactorEmailAsync(User user, string token);
        Task<U2fRegistration> StartU2fRegistrationAsync(User user);
        Task<bool> DeleteU2fKeyAsync(User user, int id);
        Task<bool> CompleteU2fRegistrationAsync(User user, int id, string name, string deviceResponse);
        Task SendEmailVerificationAsync(User user);
        Task<IdentityResult> ConfirmEmailAsync(User user, string token);
        Task InitiateEmailChangeAsync(User user, string newEmail);
        Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword,
            string token, string key);
        Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword, string key);
        Task<IdentityResult> SetPasswordAsync(User user, string newMasterPassword, string key);
        Task<IdentityResult> ChangeKdfAsync(User user, string masterPassword, string newMasterPassword, string key,
            KdfType kdf, int kdfIterations);
        Task<IdentityResult> UpdateKeyAsync(User user, string masterPassword, string key, string privateKey,
            IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
        Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
        Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type);
        Task DisableTwoFactorProviderAsync(User user, TwoFactorProviderType type,
            IOrganizationService organizationService);
        Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode,
            IOrganizationService organizationService);
        Task<string> GenerateUserTokenAsync(User user, string tokenProvider, string purpose);
        Task<IdentityResult> DeleteAsync(User user);
        Task<IdentityResult> DeleteAsync(User user, string token);
        Task SendDeleteConfirmationAsync(string email);
        Task<Tuple<bool, string>> SignUpPremiumAsync(User user, string paymentToken,
            PaymentMethodType paymentMethodType, short additionalStorageGb, UserLicense license,
            TaxInfo taxInfo);
        Task IapCheckAsync(User user, PaymentMethodType paymentMethodType);
        Task UpdateLicenseAsync(User user, UserLicense license);
        Task<string> AdjustStorageAsync(User user, short storageAdjustmentGb);
        Task ReplacePaymentMethodAsync(User user, string paymentToken, PaymentMethodType paymentMethodType, TaxInfo taxInfo);
        Task CancelPremiumAsync(User user, bool? endOfPeriod = null, bool accountDelete = false);
        Task ReinstatePremiumAsync(User user);
        Task EnablePremiumAsync(Guid userId, DateTime? expirationDate);
        Task EnablePremiumAsync(User user, DateTime? expirationDate);
        Task DisablePremiumAsync(Guid userId, DateTime? expirationDate);
        Task DisablePremiumAsync(User user, DateTime? expirationDate);
        Task UpdatePremiumExpirationAsync(Guid userId, DateTime? expirationDate);
        Task<UserLicense> GenerateLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null);
        Task<bool> CheckPasswordAsync(User user, string password);
        Task<bool> CanAccessPremium(ITwoFactorProvidersUser user);
        Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user);
        Task<bool> TwoFactorProviderIsEnabledAsync(TwoFactorProviderType provider, ITwoFactorProvidersUser user);
        Task<string> GenerateEnterprisePortalSignInTokenAsync(User user);
    }
}
