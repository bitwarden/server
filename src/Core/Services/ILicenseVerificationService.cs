using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ILicenseVerificationService
    {
        bool VerifyOrganizationPlan(Organization organization);
        bool VerifyUserPremium(User user);
    }
}
