using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ILicensingService
    {
        bool VerifyOrganizationPlan(Organization organization);
        bool VerifyUserPremium(User user);
        bool VerifyLicense(ILicense license);
        byte[] SignLicense(ILicense license);
    }
}
