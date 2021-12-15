using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.Mail
{
    public interface IOrganizationUserMailer
    {
        Task SendInvitesAsync(IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites, Organization organization);
        Task SendOrganizationAutoscaledEmailAsync(Organization organization, int initialSeatCount);
        Task SendOrganizationMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount);
    }
}
