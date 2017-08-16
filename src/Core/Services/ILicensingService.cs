using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface ILicensingService
    {
        bool VerifyOrganizationPlan(Organization organization);
        Task<bool> VerifyUserPremiumAsync(User user);
        bool VerifyLicense(ILicense license);
        byte[] SignLicense(ILicense license);
    }
}
