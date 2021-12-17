using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.UserInvite;

namespace Bit.Core.OrganizationFeatures
{
    public interface IOrganizationAccessPolicies
    {
        AccessPolicyResult CanReplacePaymentMethod(Organization organization);
        AccessPolicyResult CanVerifyBank(Organization organization);
        Task<AccessPolicyResult> CanSignUp(OrganizationSignup signup, Plan plan, bool provider);
        Task<AccessPolicyResult> CanSelfHostedSignUpAsync(OrganizationLicense license, User owner);

        Task<AccessPolicyResult> CanUpdateLicenseAsync(Organization organization, OrganizationLicense license);
    }
}
