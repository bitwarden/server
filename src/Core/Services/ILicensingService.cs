using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.SelfHostLicenses;
using Bit.Core.Billing.SelfHostLicenses.OrganizationLicenses;
using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface ILicensingService
{
    Task ValidateOrganizationsAsync();
    Task ValidateUsersAsync();
    Task<bool> ValidateUserPremiumAsync(User user);
    bool VerifyLicenseSignature(ILicense license);
    byte[] SignLicense(ILicense license);
    string GenerateToken(ILicense license);
    Task<OrganizationLicense> ReadOrganizationLicenseAsync(Organization organization);
    Task<OrganizationLicense> ReadOrganizationLicenseAsync(Guid organizationId);
    Task WriteLicenseToDiskAsync(Guid entityId, ILicense license);
}
