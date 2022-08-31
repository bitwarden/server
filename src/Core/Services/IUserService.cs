using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Business;
using Fido2NetLib;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Services;

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
    Task SendTwoFactorEmailAsync(User user, bool isBecauseNewDeviceLogin = false);
    Task<bool> VerifyTwoFactorEmailAsync(User user, string token);
    Task<CredentialCreateOptions> StartWebAuthnRegistrationAsync(User user);
    Task<bool> DeleteWebAuthnKeyAsync(User user, int id);
    Task<bool> CompleteWebAuthRegistrationAsync(User user, int value, string name, AuthenticatorAttestationRawResponse attestationResponse);
    Task SendEmailVerificationAsync(User user);
    Task<IdentityResult> ConfirmEmailAsync(User user, string token);
    Task InitiateEmailChangeAsync(User user, string newEmail);
    Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword,
        string token, string key);
    Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword, string passwordHint, string key);
    Task<IdentityResult> SetPasswordAsync(User user, string newMasterPassword, string key, string orgIdentifier = null);
    Task<IdentityResult> SetKeyConnectorKeyAsync(User user, string key, string orgIdentifier);
    Task<IdentityResult> ConvertToKeyConnectorAsync(User user);
    Task<IdentityResult> AdminResetPasswordAsync(OrganizationUserType type, Guid orgId, Guid id, string newMasterPassword, string key);
    Task<IdentityResult> UpdateTempPasswordAsync(User user, string newMasterPassword, string key, string hint);
    Task<IdentityResult> ChangeKdfAsync(User user, string masterPassword, string newMasterPassword, string key,
        KdfType kdf, int kdfIterations);
    Task<IdentityResult> UpdateKeyAsync(User user, string masterPassword, string key, string privateKey,
        IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders, IEnumerable<Send> sends);
    Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
    Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type, bool setEnabled = true, bool logEvent = true);
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
    Task<UserLicense> GenerateLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null,
        int? version = null);
    Task<bool> CheckPasswordAsync(User user, string password);
    Task<bool> CanAccessPremium(ITwoFactorProvidersUser user);
    Task<bool> HasPremiumFromOrganization(ITwoFactorProvidersUser user);
    Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user);
    Task<bool> TwoFactorProviderIsEnabledAsync(TwoFactorProviderType provider, ITwoFactorProvidersUser user);
    Task<string> GenerateSignInTokenAsync(User user, string purpose);
    Task RotateApiKeyAsync(User user);
    string GetUserName(ClaimsPrincipal principal);
    Task SendOTPAsync(User user);
    Task<bool> VerifyOTPAsync(User user, string token);
    Task<bool> VerifySecretAsync(User user, string secret);
    Task<bool> Needs2FABecauseNewDeviceAsync(User user, string deviceIdentifier, string grantType);
    bool CanEditDeviceVerificationSettings(User user);
}
