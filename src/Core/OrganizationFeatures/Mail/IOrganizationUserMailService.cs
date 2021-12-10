using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.Mail
{
    public interface IOrganizationUserMailService
    {
        Task SendInvitesAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization);
    }
}
