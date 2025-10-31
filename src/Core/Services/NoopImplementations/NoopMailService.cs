#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Mail;
using Bit.Core.Vault.Models.Data;
using Core.Auth.Enums;

namespace Bit.Core.Services;

public class NoopMailService : IMailService
{
    public Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
    {
        return Task.FromResult(0);
    }

    public Task SendVerifyEmailEmailAsync(string email, Guid userId, string hint)
    {
        return Task.FromResult(0);
    }

    public Task SendRegistrationVerificationEmailAsync(string email, string hint)
    {
        return Task.FromResult(0);
    }

    public Task SendTrialInitiationSignupEmailAsync(
        bool isExistingUser,
        string email,
        string token,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        int trailLength)
    {
        return Task.FromResult(0);
    }

    public Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendMasterPasswordHintEmailAsync(string email, string hint)
    {
        return Task.FromResult(0);
    }

    public Task SendNoMasterPasswordHintEmailAsync(string email)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount, IEnumerable<string> ownerEmails)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationAutoscaledEmailAsync(Organization organization, int initialSeatCount, IEnumerable<string> ownerEmails)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationAcceptedEmailAsync(Organization organization, string userIdentifier,
        IEnumerable<string> adminEmails, bool hasAccessSecretsManager = false)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationConfirmedEmailAsync(string organizationName, string email, bool hasAccessSecretsManager = false)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationInviteEmailsAsync(OrganizationInvitesInfo orgInvitesInfo)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(string organizationName, string email) =>
        Task.CompletedTask;

    public Task SendOrganizationUserRevokedForPolicySingleOrgEmailAsync(string organizationName, string email) =>
        Task.CompletedTask;

    public Task SendTwoFactorEmailAsync(string email, string accountEmail, string token, string deviceIp, string deviceType, TwoFactorEmailPurpose purpose)
    {
        return Task.FromResult(0);
    }

    public Task SendSendEmailOtpEmailAsync(string email, string token, string subject)
    {
        return Task.FromResult(0);
    }

    public Task SendSendEmailOtpEmailv2Async(string email, string token, string subject)
    {
        return Task.FromResult(0);
    }

    public Task SendFailedTwoFactorAttemptEmailAsync(string email, TwoFactorProviderType failedType, DateTime utcNow, string ip)
    {
        return Task.FromResult(0);
    }

    public Task SendWelcomeEmailAsync(User user)
    {
        return Task.FromResult(0);
    }

    public Task SendIndividualUserWelcomeEmailAsync(User user)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationUserWelcomeEmailAsync(User user, string organizationName)
    {
        return Task.FromResult(0);
    }

    public Task SendFreeOrgOrFamilyOrgUserWelcomeEmailAsync(User user, string familyOrganizationName)
    {
        return Task.FromResult(0);
    }
    public Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendCannotDeleteClaimedAccountEmailAsync(string email)
    {
        return Task.FromResult(0);
    }

    public Task SendPasswordlessSignInAsync(string returnUrl, string token, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendInvoiceUpcoming(
        string email,
        decimal amount,
        DateTime dueDate,
        List<string> items,
        bool mentionInvoices) => Task.FromResult(0);

    public Task SendInvoiceUpcoming(
        IEnumerable<string> emails,
        decimal amount,
        DateTime dueDate,
        List<string> items,
        bool mentionInvoices) => Task.FromResult(0);

    public Task SendProviderInvoiceUpcoming(
        IEnumerable<string> emails,
        decimal amount,
        DateTime dueDate,
        List<string> items,
        string? collectionMethod = null,
        bool hasPaymentMethod = true,
        string? paymentMethodDescription = null) => Task.FromResult(0);

    public Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices)
    {
        return Task.FromResult(0);
    }

    public Task SendAddedCreditAsync(string email, decimal amount)
    {
        return Task.FromResult(0);
    }

    public Task SendLicenseExpiredAsync(IEnumerable<string> emails, string? organizationName = null)
    {
        return Task.FromResult(0);
    }

    public Task SendNewDeviceLoggedInEmail(string email, string deviceType, DateTime timestamp, string ip)
    {
        return Task.FromResult(0);
    }

    public Task SendRecoverTwoFactorEmail(string email, DateTime timestamp, string ip)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessInviteEmailAsync(EmergencyAccess emergencyAccess, string name, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessAcceptedEmailAsync(string granteeEmail, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessConfirmedEmailAsync(string grantorName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessRecoveryInitiated(EmergencyAccess emergencyAccess, string initiatingName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessRecoveryApproved(EmergencyAccess emergencyAccess, string approvingName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessRecoveryRejected(EmergencyAccess emergencyAccess, string rejectingName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessRecoveryReminder(EmergencyAccess emergencyAccess, string initiatingName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEmergencyAccessRecoveryTimedOut(EmergencyAccess ea, string initiatingName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendEnqueuedMailMessageAsync(IMailQueueMessage queueMessage)
    {
        return Task.FromResult(0);
    }

    public Task SendAdminResetPasswordEmailAsync(string email, string userName, string orgName)
    {
        return Task.FromResult(0);
    }

    public Task SendProviderSetupInviteEmailAsync(Provider provider, string token, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendBusinessUnitConversionInviteAsync(Organization organization, string token, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendProviderInviteEmailAsync(string providerName, ProviderUser providerUser, string token, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendProviderConfirmedEmailAsync(string providerName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendProviderUserRemoved(string providerName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendProviderUpdatePaymentMethod(Guid organizationId, string organizationName, string providerName,
        IEnumerable<string> emails) => Task.FromResult(0);

    public Task SendUpdatedTempPasswordEmailAsync(string email, string userName)
    {
        return Task.FromResult(0);
    }

    public Task SendFamiliesForEnterpriseOfferEmailAsync(string SponsorOrgName, string email, bool existingAccount, string token)
    {
        return Task.FromResult(0);
    }

    public Task BulkSendFamiliesForEnterpriseOfferEmailAsync(string SponsorOrgName, IEnumerable<(string Email, bool ExistingAccount, string Token)> invites)
    {
        return Task.FromResult(0);
    }

    public Task SendFamiliesForEnterpriseRedeemedEmailsAsync(string familyUserEmail, string sponsorEmail)
    {
        return Task.FromResult(0);
    }

    public Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(string email, DateTime expirationDate)
    {
        return Task.FromResult(0);
    }

    public Task SendOTPEmailAsync(string email, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendUnclaimedOrganizationDomainEmailAsync(IEnumerable<string> adminEmails, string organizationId, string domainName)
    {
        return Task.FromResult(0);
    }

    public Task SendSecretsManagerMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount,
        IEnumerable<string> ownerEmails)
    {
        return Task.FromResult(0);
    }

    public Task SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(Organization organization,
        int maxSeatCount,
        IEnumerable<string> ownerEmails)
    {
        return Task.FromResult(0);
    }

    public Task SendTrustedDeviceAdminApprovalEmailAsync(string email, DateTime utcNow, string ip, string deviceTypeAndIdentifier)
    {
        return Task.FromResult(0);
    }

    public Task SendTrialInitiationEmailAsync(string email)
    {
        return Task.FromResult(0);
    }

    public Task SendInitiateDeletProviderEmailAsync(string email, Provider provider, string token) => throw new NotImplementedException();

    public Task SendInitiateDeleteOrganzationEmailAsync(string email, Organization organization, string token)
    {
        return Task.FromResult(0);
    }
    public Task SendRequestSMAccessToAdminEmailAsync(IEnumerable<string> adminEmails, string organizationName, string userRequestingAccess, string emailContent) => throw new NotImplementedException();

    public Task SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync(string email, string offerAcceptanceDate,
        string organizationId,
        string organizationName)
    {
        return Task.FromResult(0);
    }
    public Task SendClaimedDomainUserEmailAsync(ClaimedUserDomainClaimedEmails emailList) => Task.CompletedTask;

    public Task SendDeviceApprovalRequestedNotificationEmailAsync(IEnumerable<string> adminEmails, Guid organizationId, string email, string? userName)
    {
        return Task.FromResult(0);
    }

    public Task SendBulkSecurityTaskNotificationsAsync(Organization org, IEnumerable<UserSecurityTasksCount> securityTaskNotifications, IEnumerable<string> adminOwnerEmails)
    {
        return Task.FromResult(0);
    }
}
