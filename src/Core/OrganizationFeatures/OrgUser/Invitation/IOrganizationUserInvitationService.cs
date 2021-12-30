using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation
{
    public interface IOrganizationUserInvitationService
    {
        ExpiringToken MakeToken(OrganizationUser orgUser);
        bool TokenIsValid(string token, User user, OrganizationUser orgUser);
        Task<List<OrganizationUser>> InviteUsersAsync(Organization organization,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites,
            HashSet<string> existingUserEmails = null);
    }
}
