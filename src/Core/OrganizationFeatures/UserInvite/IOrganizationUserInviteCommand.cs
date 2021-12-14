using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public interface IOrganizationUserInviteCommand
    {
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, OrganizationUserInvite invite, string externalId);
        Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites);
    }
}
