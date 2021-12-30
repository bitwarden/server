using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.ResendInvite
{
    public interface IOrganizationUserResendInviteCommand
    {
        Task<IEnumerable<(OrganizationUser orgUser, string failureReason)>> ResendInvitesAsync(Guid organizationId,
            IEnumerable<Guid> organizationUsersId);
    }
}
