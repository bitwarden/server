using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

public interface IGetPendingAnnualUpgradeQuery
{
    Task<PendingAnnualUpgrade?> Run(Organization organization);
}
