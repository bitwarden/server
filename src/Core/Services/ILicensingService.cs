using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Services;

public interface ILicensingService
{
    Task ValidateOrganizationsAsync();
    Task ValidateUsersAsync();
    Task<bool> ValidateUserPremiumAsync(User user);
    bool VerifyLicense(ILicense license);
    byte[] SignLicense(ILicense license);
    Task<OrganizationLicense> ReadOrganizationLicenseAsync(Organization organization);
    Task<OrganizationLicense> ReadOrganizationLicenseAsync(Guid organizationId);
    ClaimsPrincipal GetClaimsPrincipalFromLicense(ILicense license);

    Task<string> CreateOrganizationTokenAsync(
        Organization organization,
        Guid installationId,
        SubscriptionInfo subscriptionInfo
    );

    Task<string> CreateUserTokenAsync(User user, SubscriptionInfo subscriptionInfo);
}
