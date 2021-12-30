using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Accept
{
    public interface IOrganizationUserAcceptAccessPolicies
    {
        Task<AccessPolicyResult> CanAcceptInviteAsync(Organization organization, User user, OrganizationUser organizationUser,
            bool tokenIsValid);
    }
}
