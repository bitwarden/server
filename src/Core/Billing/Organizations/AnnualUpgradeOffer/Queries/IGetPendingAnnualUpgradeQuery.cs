using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

public interface IGetPendingAnnualUpgradeQuery
{
    /// <summary>
    /// The annual plan and line items an organization switches to at renewal after redeeming the
    /// annual upgrade offer, or null when no such switch is scheduled.
    /// </summary>
    Task<PendingAnnualUpgrade?> Run(Organization organization);
}
