using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;

public interface IOrganizationUpgradePlanCommand
{
    Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade);
}
