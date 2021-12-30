using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Accept
{
    public interface IOrganizationUserAcceptCommand
    {
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token);
    }
}
