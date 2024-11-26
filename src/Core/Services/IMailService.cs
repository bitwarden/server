using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Auth.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;

namespace Bit.Core.Services;

public interface IMailService
{
    Task SendWelcomeEmailAsync(User user);
    Task SendVerifyEmailEmailAsync(string email, Guid userId, string token);
    Task SendRegistrationVerificationEmailAsync(string email, string token);
    Task SendTrialInitiationSignupEmailAsync(
        string email,
        string token,
        ProductTierType productTier,
        IEnumerable<ProductType> products);
    Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token);
    Task SendCannotDeleteManagedAccountEmailAsync(string email);
    Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail);
    Task SendChangeEmailEmailAsync(string newEmailAddress, string token);
    Task SendTwoFactorEmailAsync(string email, string token);
    Task SendNoMasterPasswordHintEmailAsync(string email);
    Task SendMasterPasswordHintEmailAsync(string email, string hint);

    /// <summary>
    /// Sends one or many organization invite emails.
    /// </summary>
    /// <param name="orgInvitesInfo">The information required to send the organization invites.</param>
    Task SendOrganizationInviteEmailsAsync(OrganizationInvitesInfo orgInvitesInfo);
    Task SendOrganizationMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount, IEnumerable<string> ownerEmails);
    Task SendOrganizationAutoscaledEmailAsync(Organization organization, int initialSeatCount, IEnumerable<string> ownerEmails);
    Task SendOrganizationAcceptedEmailAsync(Organization organization, string userIdentifier, IEnumerable<string> adminEmails, bool hasAccessSecretsManager = false);
    Task SendOrganizationConfirmedEmailAsync(string organizationName, string email, bool hasAccessSecretsManager = false);
    Task SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(string organizationName, string email);
    Task SendOrganizationUserRevokedForTwoFactoryPolicyEmailAsync(string organizationName, string email);
    Task SendOrganizationUserRevokedForPolicySingleOrgEmailAsync(string organizationName, string email);
    Task SendPasswordlessSignInAsync(string returnUrl, string token, string email);
    Task SendInvoiceUpcoming(
        string email,
        decimal amount,
        DateTime dueDate,
        List<string> items,
        bool mentionInvoices);
    Task SendInvoiceUpcoming(
        IEnumerable<string> email,
        decimal amount,
        DateTime dueDate,
        List<string> items,
        bool mentionInvoices);
    Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices);
    Task SendAddedCreditAsync(string email, decimal amount);
    Task SendLicenseExpiredAsync(IEnumerable<string> emails, string organizationName = null);
    Task SendNewDeviceLoggedInEmail(string email, string deviceType, DateTime timestamp, string ip);
    Task SendRecoverTwoFactorEmail(string email, DateTime timestamp, string ip);
    Task SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(string organizationName, string email);
    Task SendEmergencyAccessInviteEmailAsync(EmergencyAccess emergencyAccess, string name, string token);
    Task SendEmergencyAccessAcceptedEmailAsync(string granteeEmail, string email);
    Task SendEmergencyAccessConfirmedEmailAsync(string grantorName, string email);
    Task SendEmergencyAccessRecoveryInitiated(EmergencyAccess emergencyAccess, string initiatingName, string email);
    Task SendEmergencyAccessRecoveryApproved(EmergencyAccess emergencyAccess, string approvingName, string email);
    Task SendEmergencyAccessRecoveryRejected(EmergencyAccess emergencyAccess, string rejectingName, string email);
    Task SendEmergencyAccessRecoveryReminder(EmergencyAccess emergencyAccess, string initiatingName, string email);
    Task SendEmergencyAccessRecoveryTimedOut(EmergencyAccess ea, string initiatingName, string email);
    Task SendEnqueuedMailMessageAsync(IMailQueueMessage queueMessage);
    Task SendAdminResetPasswordEmailAsync(string email, string userName, string orgName);
    Task SendProviderSetupInviteEmailAsync(Provider provider, string token, string email);
    Task SendProviderInviteEmailAsync(string providerName, ProviderUser providerUser, string token, string email);
    Task SendProviderConfirmedEmailAsync(string providerName, string email);
    Task SendProviderUserRemoved(string providerName, string email);
    Task SendProviderUpdatePaymentMethod(
        Guid organizationId,
        string organizationName,
        string providerName,
        IEnumerable<string> emails);
    Task SendUpdatedTempPasswordEmailAsync(string email, string userName);
    Task SendFamiliesForEnterpriseOfferEmailAsync(string sponsorOrgName, string email, bool existingAccount, string token);
    Task BulkSendFamiliesForEnterpriseOfferEmailAsync(string SponsorOrgName, IEnumerable<(string Email, bool ExistingAccount, string Token)> invites);
    Task SendFamiliesForEnterpriseRedeemedEmailsAsync(string familyUserEmail, string sponsorEmail);
    Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(string email, DateTime expirationDate);
    Task SendOTPEmailAsync(string email, string token);
    Task SendFailedLoginAttemptsEmailAsync(string email, DateTime utcNow, string ip);
    Task SendFailedTwoFactorAttemptsEmailAsync(string email, DateTime utcNow, string ip);
    Task SendUnverifiedOrganizationDomainEmailAsync(IEnumerable<string> adminEmails, string organizationId, string domainName);
    Task SendSecretsManagerMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount, IEnumerable<string> ownerEmails);
    Task SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(Organization organization, int maxSeatCount, IEnumerable<string> ownerEmails);
    Task SendTrustedDeviceAdminApprovalEmailAsync(string email, DateTime utcNow, string ip, string deviceTypeAndIdentifier);
    Task SendTrialInitiationEmailAsync(string email);
    Task SendInitiateDeletProviderEmailAsync(string email, Provider provider, string token);
    Task SendInitiateDeleteOrganzationEmailAsync(string email, Organization organization, string token);
    Task SendRequestSMAccessToAdminEmailAsync(IEnumerable<string> adminEmails, string organizationName, string userRequestingAccess, string emailContent);
    Task SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync(string email, string offerAcceptanceDate, string organizationId,
        string organizationName);
}

