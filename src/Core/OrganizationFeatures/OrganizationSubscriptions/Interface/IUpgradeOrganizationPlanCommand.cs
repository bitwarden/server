using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IUpgradeOrganizationPlanCommand
{
    Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade);
}
