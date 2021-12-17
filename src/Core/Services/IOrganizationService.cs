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
        Task ReplaceAndUpdateCache(Organization org, EventType? orgEvent = null);
        Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken, PaymentMethodType paymentMethodType,
            TaxInfo taxInfo);
        Task VerifyBankAsync(Guid organizationId, int amount1, int amount2);
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup, bool provider = false);
        Task<Tuple<Organization, OrganizationUser>> SelfHostedSignUpAsync(OrganizationLicense license, User owner,
            string ownerKey, string collectionName, string publicKey, string privateKey);
        Task UpdateLicenseAsync(Guid organizationId, OrganizationLicense license);
        Task DeleteAsync(Organization organization);
        Task EnableAsync(Guid organizationId, DateTime? expirationDate);
        Task DisableAsync(Guid organizationId, DateTime? expirationDate);
        Task UpdateExpirationDateAsync(Guid organizationId, DateTime? expirationDate);
        Task UpdateAsync(Organization organization, bool updateBilling = false);
        Task UpdateTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
        Task DisableTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
        [Obsolete("This method is slated for removal once UserService is restructured. Do not create new dependencies on this method")]
        Task<OrganizationUser> AcceptUserAsync(string orgIdentifier, User user, IUserService userService);
        [Obsolete("This method is slated for removal once UserService is restructured. Do not create new dependencies on this method")]
        Task DeleteUserAsync(Guid organizationId, Guid userId);
        Task<OrganizationLicense> GenerateLicenseAsync(Guid organizationId, Guid installationId);
        Task<OrganizationLicense> GenerateLicenseAsync(Organization organization, Guid installationId,
            int? version = null);
        Task RotateApiKeyAsync(Organization organization);
        Task DeleteSsoUserAsync(Guid userId, Guid? organizationId);
        Task<Organization> UpdateOrganizationKeysAsync(Guid orgId, string publicKey, string privateKey);
        Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, bool includeProvider = true);
    }
}
