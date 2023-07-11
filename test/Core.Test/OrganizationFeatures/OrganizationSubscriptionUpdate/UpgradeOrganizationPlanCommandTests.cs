using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Entities.Organization;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class UpgradeOrganizationPlanCommandTests
{
    [Theory, BitAutoData]
    public async Task UpgradePlan_OrganizationIsNull_Throws(Guid organizationId, OrganizationUpgrade upgrade,
            SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(Task.FromResult<Organization>(null));
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpgradePlanAsync(organizationId, upgrade));
    }

    [Theory, BitAutoData]
    public async Task UpgradePlan_GatewayCustomIdIsNull_Throws(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        organization.GatewayCustomerId = string.Empty;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("no payment method", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpgradePlan_AlreadyInPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.Plan = organization.PlanType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("already on this plan", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpgradePlan_SM_AlreadyInPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
        SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.Plan = organization.PlanType;
        upgrade.UseSecretsManager = true;
        upgrade.AdditionalSmSeats = 10;
        upgrade.AdditionalServiceAccounts = 10;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("already on this plan", exception.Message);
    }

    [Theory, PaidOrganizationCustomize(CheckedPlanType = PlanType.Free), BitAutoData]
    public async Task UpgradePlan_UpgradeFromPaidPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("can only upgrade", exception.Message);
    }

    [Theory, PaidOrganizationCustomize(CheckedPlanType = PlanType.Free), BitAutoData]
    public async Task UpgradePlan_SM_UpgradeFromPaidPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
        SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.UseSecretsManager = true;
        upgrade.AdditionalSmSeats = 10;
        upgrade.AdditionalServiceAccounts = 10;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("can only upgrade", exception.Message);
    }

    [Theory]
    [FreeOrganizationUpgradeCustomize, BitAutoData]
    public async Task UpgradePlan_Passes(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        upgrade.AdditionalSmSeats = 10;
        upgrade.AdditionalSeats = 10;
        await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);
        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Theory, FreeOrganizationUpgradeCustomize]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpgradePlan_SM_Passes(PlanType planType, Organization organization, OrganizationUpgrade upgrade,
        SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.Plan = planType;

        var passwordManagerPlan = StaticStore.GetPasswordManagerPlan(upgrade.Plan);
        var secretsManagerPlan = StaticStore.GetSecretsManagerPlan(upgrade.Plan);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        upgrade.AdditionalSeats = 15;
        upgrade.AdditionalSmSeats = 10;
        upgrade.AdditionalServiceAccounts = 20;

        var result = await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
            Arg.Is<Organization>(o =>
                o.Seats == passwordManagerPlan.BaseSeats + upgrade.AdditionalSeats
                && o.SmSeats == secretsManagerPlan.BaseSeats + upgrade.AdditionalSmSeats
                && o.SmServiceAccounts == secretsManagerPlan.BaseServiceAccount + upgrade.AdditionalServiceAccounts));

        Assert.True(result.Item1);
        Assert.NotNull(result.Item2);
    }

}
