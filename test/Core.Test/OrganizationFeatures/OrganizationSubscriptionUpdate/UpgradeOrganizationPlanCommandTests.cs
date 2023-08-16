using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
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

        var plan = StaticStore.GetPlan(upgrade.Plan);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        upgrade.AdditionalSeats = 15;
        upgrade.AdditionalSmSeats = 10;
        upgrade.AdditionalServiceAccounts = 20;

        var result = await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + upgrade.AdditionalSeats
                && o.SmSeats == plan.SecretsManager.BaseSeats + upgrade.AdditionalSmSeats
                && o.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + upgrade.AdditionalServiceAccounts));

        Assert.True(result.Item1);
        Assert.NotNull(result.Item2);
    }


    [Theory, FreeOrganizationUpgradeCustomize]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpgradePlan_SM_NotEnoughSmSeats_Throws(PlanType planType, Organization organization, OrganizationUpgrade upgrade,
        SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.Plan = planType;
        upgrade.AdditionalSeats = 15;
        upgrade.AdditionalSmSeats = 1;
        upgrade.AdditionalServiceAccounts = 0;

        organization.SmSeats = 2;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id).Returns(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains("Your organization currently has 2 Secrets Manager seats filled. Your new plan only has", exception.Message);

        sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs().ReplaceAndUpdateCacheAsync(default);
    }

    [Theory, FreeOrganizationUpgradeCustomize]
    [BitAutoData(PlanType.EnterpriseMonthly, 201)]
    [BitAutoData(PlanType.EnterpriseAnnually, 201)]
    [BitAutoData(PlanType.TeamsMonthly, 51)]
    [BitAutoData(PlanType.TeamsAnnually, 51)]
    public async Task UpgradePlan_SM_NotEnoughServiceAccounts_Throws(PlanType planType, int currentServiceAccounts,
     Organization organization, OrganizationUpgrade upgrade, SutProvider<UpgradeOrganizationPlanCommand> sutProvider)
    {
        upgrade.Plan = planType;
        upgrade.AdditionalSeats = 15;
        upgrade.AdditionalSmSeats = 1;
        upgrade.AdditionalServiceAccounts = 0;

        organization.SmSeats = 1;
        organization.SmServiceAccounts = currentServiceAccounts;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id).Returns(1);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id).Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
        Assert.Contains($"Your organization currently has {currentServiceAccounts} service accounts. Your new plan only allows", exception.Message);

        sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs().ReplaceAndUpdateCacheAsync(default);
    }
}
