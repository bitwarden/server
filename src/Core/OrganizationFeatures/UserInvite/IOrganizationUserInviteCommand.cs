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
        Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites);
    }
}
