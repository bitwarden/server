using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    public interface IOrganizationUserConfirmCommand
    {
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key);
        Task<List<(OrganizationUser orgUser, string error)>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> orgUserKeys);
    }
}
