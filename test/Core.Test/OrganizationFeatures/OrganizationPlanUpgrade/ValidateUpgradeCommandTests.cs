using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;
using Bit.Core.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationPlanUpgrade;

[SutProviderCustomize]
public class ValidateUpgradeCommandTests
{
    [Theory]
    [BitAutoData]
    public void ValidatePlan_ExistingPlanIsNull_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan newPlan)
    {
        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(newPlan, null));
    }

    [Theory]
    [BitAutoData]
    public void ValidatePlan_NewPlanIsNull_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan existingPlan)
    {
        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(null, existingPlan));
    }

    [Theory]
    [BitAutoData]
    public void ValidatePlan_NewPlanIsDisabled_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan existingPlan, Plan newPlan)
    {
        newPlan = new Plan { Disabled = true };

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(newPlan, existingPlan));
    }

    [Theory]
    [BitAutoData]
    public void ValidatePlan_ExistingPlanTypeIsSameAsNewPlanType_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan existingPlan, Plan newPlan)
    {
        existingPlan = new Plan { Type = PlanType.EnterpriseAnnually };
        newPlan = new Plan { Type = PlanType.EnterpriseAnnually };

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(newPlan, existingPlan));
    }

    [Theory]
    [BitAutoData]
    public void
        ValidatePlan_ExistingPlanUpgradeSortOrderIsGreaterThanOrEqualToNewPlanUpgradeSortOrder_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan existingPlan, Plan newPlan)
    {
        existingPlan = new Plan { UpgradeSortOrder = 2 };
        newPlan = new Plan { UpgradeSortOrder = 1 };

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(newPlan, existingPlan));
    }

    [Theory]
    [BitAutoData]
    public void ValidatePlan_ExistingPlanTypeIsNotFree_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider, Plan existingPlan, Plan newPlan)
    {
        existingPlan = new Plan { Type = PlanType.TeamsAnnually, UpgradeSortOrder = 2 };
        newPlan = new Plan { Type = PlanType.EnterpriseAnnually, UpgradeSortOrder = 3 };

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidatePlan(newPlan, existingPlan));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateSeatsAsync_OrganizationSeatsIsGreaterThanNewPlanSeats_OccupiedSeatCountNotChecked(
        SutProvider<ValidateUpgradeCommand> sutProvider, Organization organization, Plan passwordManagerPlan, OrganizationUpgrade upgrade)
    {
        organization = new Organization { Seats = 5 };
        passwordManagerPlan = new Plan { BaseSeats = 5 };

        await sutProvider.Sut.ValidateSeatsAsync(organization, passwordManagerPlan, upgrade);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceive().GetOccupiedSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateSmSeatsAsync_OrganizationSmSeatsIsGreaterThanNewPlanSeats_OccupiedSmSeatCountNotChecked(
        SutProvider<ValidateUpgradeCommand> sutProvider, Organization organization, Plan newPlan, OrganizationUpgrade upgrade)
    {
        organization = new Organization { SmSeats = 5 };
        newPlan = new Plan { BaseSeats = 5 };
        upgrade = new OrganizationUpgrade();

        await sutProvider.Sut.ValidateSmSeatsAsync(organization, newPlan, upgrade);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceive().GetOccupiedSmSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateCollectionsAsync_UpgradePlanAllowsAddingCollections_CollectionCountDoesNotExceedLimit(
        SutProvider<ValidateUpgradeCommand> sutProvider, Organization organization, Plan newPlan)
    {
        organization = new Organization { Id = Guid.NewGuid() };
        newPlan = new Plan { MaxCollections = 5 };
        sutProvider.GetDependency<ICollectionRepository>().GetCountByOrganizationIdAsync(organization.Id).Returns(3);

        await sutProvider.Sut.ValidateCollectionsAsync(organization, newPlan);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateGroupsAsync_NewPlanDoesNotAllowGroupsAndOrganizationHasGroups_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider)
    {
        var organization = new Organization { Id = Guid.NewGuid(), UseGroups = true };
        var newPlan = new Plan { HasGroups = false };
        var groups = new List<Group> { new Group() };

        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(groups);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateGroupsAsync(organization, newPlan));
        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidatePoliciesAsync_NewPlanDoesNotAllowPoliciesAndOrganizationHasEnabledPolicies_ThrowsBadRequestException(
        SutProvider<ValidateUpgradeCommand> sutProvider)
    {
        var organization = new Organization { Id = Guid.NewGuid(), UsePolicies = true };
        var newPlan = new Plan { HasPolicies = false };
        var policies = new List<Policy> { new Policy { Enabled = true } };

        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(policies);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidatePoliciesAsync(organization, newPlan));

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).GetManyByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateSsoAsync_NewPlanDoesNotAllowSsoAndOrganizationHasEnabledSsoConfig_ThrowsBadRequestException(SutProvider<ValidateUpgradeCommand> sutProvider)
    {
        var organization = new Organization { Id = Guid.NewGuid(), UseSso = true };
        var newPlan = new Plan { HasSso = false };
        var ssoConfig = new SsoConfig { Enabled = true };

        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateSsoAsync(organization, newPlan));

        await sutProvider.GetDependency<ISsoConfigRepository>().Received(1).GetByOrganizationIdAsync(organization.Id);
    }

}
