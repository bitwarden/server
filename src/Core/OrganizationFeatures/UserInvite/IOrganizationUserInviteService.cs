using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Services.OrganizationServices.UserInvite
{
    public interface IOrganizationUserInviteService
    {
        Task<List<OrganizationUser>> InviteUsersAsync(Organization organization,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites,
            HashSet<string> existingUserEmails = null);
    }
}
