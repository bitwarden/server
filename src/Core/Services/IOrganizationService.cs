using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken, PaymentMethodType paymentMethodType,
            TaxInfo taxInfo);
        Task CancelSubscriptionAsync(Guid organizationId, bool? endOfPeriod = null);
        Task ReinstateSubscriptionAsync(Guid organizationId);
        Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade);
        Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb);
        Task<string> AdjustSeatsAsync(Guid organizationId, int seatAdjustment);
        Task VerifyBankAsync(Guid organizationId, int amount1, int amount2);
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationLicense license, User owner,
            string ownerKey, string collectionName);
        Task UpdateLicenseAsync(Guid organizationId, OrganizationLicense license);
        Task DeleteAsync(Organization organization);
        Task EnableAsync(Guid organizationId, DateTime? expirationDate);
        Task DisableAsync(Guid organizationId, DateTime? expirationDate);
        Task UpdateExpirationDateAsync(Guid organizationId, DateTime? expirationDate);
        Task EnableAsync(Guid organizationId);
        Task UpdateAsync(Organization organization, bool updateBilling = false);
        Task UpdateTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
        Task DisableTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, string email,
            OrganizationUserType type, bool accessAll, string externalId, IEnumerable<SelectionReadOnly> collections);
        Task<List<OrganizationUser>> InviteUserAsync(Guid organizationId, Guid? invitingUserId, string externalId, OrganizationUserInvite orgUserInvite);
        Task ResendInviteAsync(Guid organizationId, Guid? invitingUserId, Guid organizationUserId);
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token,
            IUserService userService);
        Task<OrganizationUser> AcceptUserAsync(string orgIdentifier, User user, IUserService userService);
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
            Guid confirmingUserId, IUserService userService);
        Task SaveUserAsync(OrganizationUser user, Guid? savingUserId, IEnumerable<SelectionReadOnly> collections);
        Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);
        Task DeleteUserAsync(Guid organizationId, Guid userId);
        Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId);
        Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid organizationUserId, string resetPasswordKey);
        Task<OrganizationLicense> GenerateLicenseAsync(Guid organizationId, Guid installationId);
        Task<OrganizationLicense> GenerateLicenseAsync(Organization organization, Guid installationId,
            int? version = null);
        Task ImportAsync(Guid organizationId, Guid? importingUserId, IEnumerable<ImportedGroup> groups,
            IEnumerable<ImportedOrganizationUser> newUsers, IEnumerable<string> removeUserExternalIds,
            bool overwriteExisting);
        Task RotateApiKeyAsync(Organization organization);
        Task DeleteSsoUserAsync(Guid userId, Guid? organizationId);
    }
}
