using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Business;
using Bit.Core.Models.Mail;

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

    public Task SendOrganizationAcceptedEmailAsync(Organization organization, string userIdentifier, IEnumerable<string> adminEmails)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, ExpiringToken token)
    {
        return Task.FromResult(0);
    }

    public Task BulkSendOrganizationInviteEmailAsync(string organizationName, IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites)
    {
        return Task.FromResult(0);
    }

    public Task SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(string organizationName, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendTwoFactorEmailAsync(string email, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendNewDeviceLoginTwoFactorEmailAsync(string email, string token)
    {
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(User user)
    {
        return Task.FromResult(0);
    }

    public Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
    {
        return Task.FromResult(0);
    }

    public Task SendPasswordlessSignInAsync(string returnUrl, string token, string email)
    {
        return Task.FromResult(0);
    }

    public Task SendInvoiceUpcomingAsync(string email, decimal amount, DateTime dueDate,
        List<string> items, bool mentionInvoices)
    {
        return Task.FromResult(0);
    }

    public Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices)
    {
        return Task.FromResult(0);
    }

    public Task SendAddedCreditAsync(string email, decimal amount)
    {
        return Task.FromResult(0);
    }

    public Task SendLicenseExpiredAsync(IEnumerable<string> emails, string organizationName = null)
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

    public Task SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(string organizationName, string email)
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

    public Task SendFailedLoginAttemptsEmailAsync(string email, DateTime utcNow, string ip)
    {
        return Task.FromResult(0);
    }

    public Task SendFailedTwoFactorAttemptsEmailAsync(string email, DateTime utcNow, string ip)
    {
        return Task.FromResult(0);
    }
}
