namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IUpgradeOrganizationPlanCommand
{
    Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, Models.Business.OrganizationUpgrade upgrade);
}
