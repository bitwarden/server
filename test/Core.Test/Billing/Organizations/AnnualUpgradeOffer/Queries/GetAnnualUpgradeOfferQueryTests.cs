using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetAnnualUpgradeOfferQueryTests
{
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IGetChurnOfferCohortMembershipQuery _getChurnOfferCohortMembershipQuery =
        Substitute.For<IGetChurnOfferCohortMembershipQuery>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<GetAnnualUpgradeOfferQuery> _logger =
        Substitute.For<ILogger<GetAnnualUpgradeOfferQuery>>();
    private readonly GetAnnualUpgradeOfferQuery _query;

    public GetAnnualUpgradeOfferQueryTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _query = new GetAnnualUpgradeOfferQuery(
            _logger, _featureService, _getChurnOfferCohortMembershipQuery, _pricingClient, _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    private Subscription SetupSubscription(Organization organization, params SubscriptionItem[] items)
    {
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem> { Data = [.. items] }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        return subscription;
    }

    [Fact]
    public async Task Run_FlagDisabled_ReturnsNull_WithoutAnyLookups()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);
        var organization = CreateOrganization(PlanType.TeamsMonthly);

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _getChurnOfferCohortMembershipQuery.DidNotReceive().Run(Arg.Any<Organization>());
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_OrgInChurnOfferCohort_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
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
        var organization = CreateOrganization(planType);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_NoGatewaySubscriptionId_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        organization.GatewaySubscriptionId = null;
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_MonthlyTeamsOrg_ReturnsSavingsFromBilledQuantity()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // 20 purchased seats on the subscription; savings must quote what Stripe bills,
        // not the occupied-seat count.
        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        var expectedCurrentAnnualCost = monthlyPlan.PasswordManager.SeatPrice * 20 * 12;
        var expectedNewAnnualCost = annualPlan.PasswordManager.SeatPrice * 20;
        Assert.Equal(expectedCurrentAnnualCost, result.CurrentAnnualCost);
        Assert.Equal(expectedNewAnnualCost, result.NewAnnualCost);
        Assert.Equal(expectedCurrentAnnualCost - expectedNewAnnualCost, result.Savings);
        Assert.True(result.Savings > 0);
    }

    [Fact]
    public async Task Run_LegacyVintageMonthlyOrg_ComparesAgainstAnnualLatest()
    {
        // An org still on a legacy monthly vintage (e.g. pending a Track A price migration) has
        // savings computed against the annual-latest plan -- the same target the migration program
        // would move it to -- not the legacy-vintage annual plan. At current pricing the legacy
        // Enterprise 2020 monthly rate ($6/seat/mo = $72/seat/yr) equals the annual-latest rate
        // ($72/seat/yr), so there are no positive savings and no offer is returned.
        var organization = CreateOrganization(PlanType.EnterpriseMonthly2020);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new Enterprise2020Plan(false);
        var annualLatestPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualLatestPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 5 });

        var result = await _query.Run(organization);

        // The vintage-specific annual plan (EnterpriseAnnually2020) is never consulted.
        await _pricingClient.Received(1).GetPlanOrThrow(PlanType.EnterpriseAnnually);
        await _pricingClient.DidNotReceive().GetPlanOrThrow(PlanType.EnterpriseAnnually2020);
        Assert.Null(result);
    }

    [Fact]
    public async Task Run_SubscriptionMissing_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _query.Run(organization);

        Assert.Null(result);
        _logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Error, default, default!, default, default!);
    }

    [Fact]
    public async Task Run_NoSeatLineItem_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeStoragePlanId }, Quantity = 2 });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ActiveScheduleAlreadyTargetsAnnualPrice_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        // A redeemed org keeps its monthly PlanType until renewal; the annual-switch schedule
        // is the only durable marker of redemption.
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = monthlyPlan.PasswordManager.StripeSeatPlanId, Quantity = 20 }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = annualPlan.PasswordManager.StripeSeatPlanId, Quantity = 20 }]
                            }
                        ]
                    }
                ]
            });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ActiveScheduleTargetsMonthlyPrice_StillReturnsOffer()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        // A Track A migration schedule targets a monthly price in its future phase; it must
        // NOT suppress the offer (redeeming releases and replaces it).
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = monthlyPlan.PasswordManager.StripeSeatPlanId, Quantity = 20 }]
                            }
                        ]
                    }
                ]
            });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Run_AnnualScheduleForDifferentSubscriptionOrInactive_StillReturnsOffer()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        // Two schedules carry the annual seat price but neither marks THIS subscription as
        // redeemed: one belongs to a different subscription, the other is not active. The offer
        // must still surface.
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        SubscriptionId = "sub_other",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = [new SubscriptionSchedulePhase { Items = [new SubscriptionSchedulePhaseItem { PriceId = annualPlan.PasswordManager.StripeSeatPlanId, Quantity = 20 }] }]
                    },
                    new SubscriptionSchedule
                    {
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Canceled,
                        Phases = [new SubscriptionSchedulePhase { Items = [new SubscriptionSchedulePhaseItem { PriceId = annualPlan.PasswordManager.StripeSeatPlanId, Quantity = 20 }] }]
                    }
                ]
            });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Run_ItemWithNullPrice_IgnoredWhenLocatingSeat_ReturnsOffer()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // A line item with no expanded Price must be skipped safely when locating the seat line.
        SetupSubscription(organization,
            new SubscriptionItem { Price = null, Quantity = 1 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(monthlyPlan.PasswordManager.SeatPrice * 20 * 12, result.CurrentAnnualCost);
    }
}
