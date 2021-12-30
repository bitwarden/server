using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite
{
    public interface IOrganizationUserInviteAccessPolicies
    {

        Task<AccessPolicyResult> CanInviteAsync(Organization organization, IEnumerable<OrganizationUserInviteData> invites, Guid? invitingUserId);
    }
}
