using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    public interface IOrganizationUserConfirmAccessPolicies
    {
        Task<AccessPolicyResult> CanConfirmUserAsync(Organization organization, User user, OrganizationUser organizationUser,
            IEnumerable<OrganizationUser> allOrgUsers = null);
    }
}
