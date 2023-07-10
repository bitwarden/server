namespace Bit.Core.OrganizationFeatures.OrganizationUpgrade;

public interface IUpgradeOrganizationPlanCommand
{
    Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, Models.Business.OrganizationUpgrade upgrade);
}
