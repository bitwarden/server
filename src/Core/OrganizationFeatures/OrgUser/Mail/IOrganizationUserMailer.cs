using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Mail
{
    public interface IOrganizationUserMailer
    {
        Task SendInvitesAsync(IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites, Organization organization);
        Task SendOrganizationAcceptedEmailAsync(Organization organization, User user);
        Task SendOrganizationConfirmedEmail(Organization organization, User user);
    }
}
