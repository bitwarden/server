namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IUpgradeOrganizationPlanCommand
{
    Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, Core.Models.Business.OrganizationUpgrade upgrade);
}
