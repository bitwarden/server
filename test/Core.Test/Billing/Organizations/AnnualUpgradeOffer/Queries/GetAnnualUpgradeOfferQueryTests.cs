using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks.Plans;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Queries;

public class GetAnnualUpgradeOfferQueryTests
{
    private readonly IGetChurnOfferCohortMembershipQuery _getChurnOfferCohortMembershipQuery =
        Substitute.For<IGetChurnOfferCohortMembershipQuery>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly GetAnnualUpgradeOfferQuery _query;

    public GetAnnualUpgradeOfferQueryTests()
    {
        _query = new GetAnnualUpgradeOfferQuery(_getChurnOfferCohortMembershipQuery, _pricingClient, _organizationRepository);
    }

    [Fact]
    public async Task Run_OrgInChurnOfferCohort_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid(), PlanType = PlanType.TeamsMonthly };
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns(
            new ChurnOfferCohortMembership(
                new OrganizationPlanMigrationCohortAssignment { Id = Guid.NewGuid(), OrganizationId = organization.Id, CohortId = Guid.NewGuid() },
                new OrganizationPlanMigrationCohort { Id = Guid.NewGuid(), Name = "cohort", IsActive = true, ChurnDiscountCouponCode = "coupon" }));

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _pricingClient.DidNotReceive().GetPlanOrThrow(Arg.Any<PlanType>());
    }

    [Theory]
    [InlineData(PlanType.TeamsAnnually)]
    [InlineData(PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.Free)]
    public async Task Run_NotAMonthlyBusinessPlan_ReturnsNull(PlanType planType)
    {
        var organization = new Organization { Id = Guid.NewGuid(), PlanType = planType };
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_MonthlyTeamsOrg_NotInCohort_ReturnsSavings()
    {
        var organization = new Organization { Id = Guid.NewGuid(), PlanType = PlanType.TeamsMonthly };
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 10 });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        var expectedCurrentAnnualCost = monthlyPlan.PasswordManager.SeatPrice * 10 * 12;
        var expectedNewAnnualCost = annualPlan.PasswordManager.SeatPrice * 10;
        Assert.Equal(expectedCurrentAnnualCost, result.CurrentAnnualCost);
        Assert.Equal(expectedNewAnnualCost, result.NewAnnualCost);
        Assert.Equal(expectedCurrentAnnualCost - expectedNewAnnualCost, result.Savings);
        Assert.True(result.Savings > 0);
    }

    [Fact]
    public async Task Run_LegacyVintageMonthlyOrg_ComparesAgainstAnnualLatest()
    {
        // An org still on a legacy monthly vintage (e.g. pending a Track A price migration) sees
        // savings computed against the annual-latest plan -- the same target the migration program
        // would move it to -- not the legacy-vintage annual plan.
        var organization = new Organization { Id = Guid.NewGuid(), PlanType = PlanType.EnterpriseMonthly2020 };
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new Enterprise2020Plan(false);
        var annualLatestPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualLatestPlan);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 5 });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(annualLatestPlan.PasswordManager.SeatPrice * 5, result.NewAnnualCost);
    }
}
