﻿using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    Task<IdentityResult> CreateUserAsync(User user);
    Task<IdentityResult> CreateUserAsync(User user, string masterPasswordHash);
    Task SendMasterPasswordHintAsync(string email);
    /// <summary>
    /// Used for both email two factor and email two factor setup.
    /// </summary>
    /// <param name="user">user requesting the action</param>
    /// <param name="authentication">this controls if what verbiage is shown in the email</param>
    /// <returns>void</returns>
    Task SendTwoFactorEmailAsync(User user, bool authentication = true);
    /// <summary>
    /// Calls the same email implementation but instead it sends the token to the account email not the
    /// email set up for two-factor, since in practice they can be different.
    /// </summary>
    /// <param name="user">user attepting to login with a new device</param>
    /// <returns>void</returns>
    Task SendNewDeviceVerificationEmailAsync(User user);
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
    Task<IdentityResult> SetKeyConnectorKeyAsync(User user, string key, string orgIdentifier);
    Task<IdentityResult> ConvertToKeyConnectorAsync(User user);
    Task<IdentityResult> AdminResetPasswordAsync(OrganizationUserType type, Guid orgId, Guid id, string newMasterPassword, string key);
    Task<IdentityResult> UpdateTempPasswordAsync(User user, string newMasterPassword, string key, string hint);
    Task<IdentityResult> ChangeKdfAsync(User user, string masterPassword, string newMasterPassword, string key,
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism);
    Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPasswordHash);
    Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type, bool setEnabled = true, bool logEvent = true);
    Task DisableTwoFactorProviderAsync(User user, TwoFactorProviderType type);
    Task<IdentityResult> DeleteAsync(User user);
    Task<IdentityResult> DeleteAsync(User user, string token);
    Task SendDeleteConfirmationAsync(string email);
    Task<Tuple<bool, string>> SignUpPremiumAsync(User user, string paymentToken,
        PaymentMethodType paymentMethodType, short additionalStorageGb, UserLicense license,
        TaxInfo taxInfo);
    Task UpdateLicenseAsync(User user, UserLicense license);
    Task<string> AdjustStorageAsync(User user, short storageAdjustmentGb);
    Task ReplacePaymentMethodAsync(User user, string paymentToken, PaymentMethodType paymentMethodType, TaxInfo taxInfo);
    Task CancelPremiumAsync(User user, bool? endOfPeriod = null);
    Task ReinstatePremiumAsync(User user);
    Task EnablePremiumAsync(Guid userId, DateTime? expirationDate);
    Task DisablePremiumAsync(Guid userId, DateTime? expirationDate);
    Task UpdatePremiumExpirationAsync(Guid userId, DateTime? expirationDate);
    Task<UserLicense> GenerateLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null,
        int? version = null);
    Task<bool> CheckPasswordAsync(User user, string password);
    Task<bool> CanAccessPremium(ITwoFactorProvidersUser user);
    Task<bool> HasPremiumFromOrganization(ITwoFactorProvidersUser user);
    [Obsolete("Use ITwoFactorIsEnabledQuery instead.")]
    Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user);
    Task<bool> TwoFactorProviderIsEnabledAsync(TwoFactorProviderType provider, ITwoFactorProvidersUser user);
    Task<string> GenerateSignInTokenAsync(User user, string purpose);

    Task<IdentityResult> UpdatePasswordHash(User user, string newPassword,
        bool validatePassword = true, bool refreshStamp = true);
    Task RotateApiKeyAsync(User user);
    string GetUserName(ClaimsPrincipal principal);
    Task SendOTPAsync(User user);
    Task<bool> VerifyOTPAsync(User user, string token);
    Task<bool> VerifySecretAsync(User user, string secret, bool isSettingMFA = false);
    Task ResendNewDeviceVerificationEmail(string email, string secret);
    /// <summary>
    /// We use this method to check if the user has an active new device verification bypass
    /// </summary>
    /// <param name="userId">self</param>
    /// <returns>returns true if the value is found in the cache</returns>
    Task<bool> ActiveNewDeviceVerificationException(Guid userId);
    /// <summary>
    /// We use this method to toggle the new device verification bypass
    /// </summary>
    /// <param name="userId">Id of user bypassing new device verification</param>
    Task ToggleNewDeviceVerificationException(Guid userId);

    void SetTwoFactorProvider(User user, TwoFactorProviderType type, bool setEnabled = true);

    [Obsolete("To be removed when the feature flag pm-17128-recovery-code-login is removed PM-18175.")]
    Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode);

    /// <summary>
    /// This method is used by the TwoFactorAuthenticationValidator to recover two
    /// factor for a user. This allows users to be logged in after a successful recovery
    /// attempt.
    ///
    /// This method logs the event, sends an email to the user, and removes two factor
    /// providers on the user account. This means that a user will have to accomplish
    /// new device verification on their account on new logins, if it is enabled for their user.
    /// </summary>
    /// <param name="recoveryCode">recovery code associated with the user logging in</param>
    /// <param name="user">The user to refresh the 2FA and Recovery Code on.</param>
    /// <returns>true if the recovery code is valid; false otherwise</returns>
    Task<bool> RecoverTwoFactorAsync(User user, string recoveryCode);

    /// <summary>
    /// Returns true if the user is a legacy user. Legacy users use their master key as their
    /// encryption key. We force these users to the web to migrate their encryption scheme.
    /// </summary>
    Task<bool> IsLegacyUser(string userId);

    /// <summary>
    /// Indicates if the user is managed by any organization.
    /// </summary>
    /// <remarks>
    /// A user is considered managed by an organization if their email domain matches one of the
    /// verified domains of that organization, and the user is a member of it.
    /// The organization must be enabled and able to have verified domains.
    /// </remarks>
    /// <returns>
    /// False if the Account Deprovisioning feature flag is disabled.
    /// </returns>
    Task<bool> IsManagedByAnyOrganizationAsync(Guid userId);

    /// <summary>
    /// Gets the organizations that manage the user.
    /// </summary>
    /// <returns>
    /// An empty collection if the Account Deprovisioning feature flag is disabled.
    /// </returns>
    /// <inheritdoc cref="IsManagedByAnyOrganizationAsync(Guid)"/>
    Task<IEnumerable<Organization>> GetOrganizationsManagingUserAsync(Guid userId);
}
