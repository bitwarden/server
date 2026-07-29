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

    // Default target for the new schedule-ownership and calculator-driven tests below, all of
    // which put the organization on the legacy TeamsMonthly2020 vintage. Teams2020Plan's own
    // annual seat price is used as the "annual-latest" plan rather than the current TeamsPlan's,
    // because TeamsPlan's annual seat price ($48/seat/yr) exactly equals the 2020 monthly rate
    // annualized ($4/seat/mo x 12), which would zero out savings for a seat-only line and break
    // the ">0 savings" gate the query applies. Using Teams2020Plan's own annual rate ($36/seat/yr)
    // keeps a genuine margin without touching any of the given test bodies.
    private readonly Teams2020Plan _currentPlan = new(isAnnual: false);
    private readonly Teams2020Plan _annualLatestPlan = new(isAnnual: true);

    public GetAnnualUpgradeOfferQueryTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(_currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(_annualLatestPlan);
        _query = new GetAnnualUpgradeOfferQuery(
            _logger, _featureService, _getChurnOfferCohortMembershipQuery,
            _pricingClient, _stripeAdapter);
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
            Currency = "usd",
            Items = new StripeList<SubscriptionItem> { Data = [.. items] }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        return subscription;
    }

    private static SubscriptionItem SeatItem(string priceId, long quantity) => new()
    {
        Id = $"si_{priceId}",
        Quantity = quantity,
        Price = new Price { Id = priceId, ProductId = $"prod_{priceId}" }
    };

    private static SubscriptionSchedule AttachSchedule(
        Subscription subscription, string id, Dictionary<string, string>? phaseMetadata)
    {
        var schedule = new SubscriptionSchedule
        {
            Id = id,
            Status = SubscriptionScheduleStatus.Active,
            Phases = [new SubscriptionSchedulePhase { Metadata = phaseMetadata }]
        };
        subscription.ScheduleId = id;
        subscription.Schedule = schedule;
        return schedule;
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
    public async Task Run_NoLineItems_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // The calculator suppresses when there are no priceable lines at all.
        SetupSubscription(organization);

        var result = await _query.Run(organization);

        Assert.Null(result);
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

    [Fact]
    public async Task Run_ForeignSchedule_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_negotiated", new Dictionary<string, string>
        {
            ["negotiated_term"] = "3y"
        });

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_annual", new Dictionary<string, string>
        {
            [MetadataKeys.AnnualUpgrade] = nameof(PlanType.TeamsMonthly2020)
        });

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_PriceMigrationSchedule_StillReturnsOffer()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_migration", new Dictionary<string, string>
        {
            [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
        });

        Assert.NotNull(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_UnexpandedSchedule_ReturnsNull_AndLogsAnError()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.ScheduleId = "sub_sched_unread";
        subscription.Schedule = null;

        Assert.Null(await _query.Run(organization));
        _logger.ReceivedWithAnyArgs().Log<object>(LogLevel.Error, default, default!, default, default!);
    }

    [Fact]
    public async Task Run_ScheduleCarryingAnnualLatestSeatPriceWithoutMetadata_ReturnsNull()
    {
        // Pins that the offer is no longer suppressed on price ID content. Such a schedule is now
        // Foreign, which also suppresses, but for the right reason and with the right log.
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        var schedule = AttachSchedule(subscription, "sub_sched_content", phaseMetadata: null);
        schedule.Phases[0].Items =
        [
            new SubscriptionSchedulePhaseItem
            {
                PriceId = _annualLatestPlan.PasswordManager.StripeSeatPlanId
            }
        ];

        Assert.Null(await _query.Run(organization));

        // Content-based classification would have logged this as an already-redeemed
        // AnnualUpgrade schedule at Information. Pin the Warning-level, unrecognized-schedule
        // log that only the Foreign classification produces, so a revert back to inspecting
        // price IDs would fail this test rather than pass it unnoticed.
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("unrecognized schedule") &&
                o.ToString()!.Contains("sub_sched_content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_ExpandsScheduleAndDiscounts()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization, SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));

        await _query.Run(organization);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(
            organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.Contains("schedule") &&
                options.Expand.Contains("customer") &&
                options.Expand.Contains("customer.discount.coupon.applies_to") &&
                options.Expand.Contains("discounts.coupon.applies_to") &&
                options.Expand.Contains("items.data.discounts.coupon")));
    }

    [Fact]
    public async Task Run_SecretsManagerLines_IncludedInBothFigures()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            SeatItem(_currentPlan.SecretsManager.StripeSeatPlanId, 3));

        var result = await _query.Run(organization);

        var expectedMonthly =
            _currentPlan.PasswordManager.SeatPrice * 5 +
            _currentPlan.SecretsManager.SeatPrice * 3;
        Assert.NotNull(result);
        Assert.Equal(expectedMonthly * 12, result!.CurrentAnnualCost);
    }

    [Fact]
    public async Task Run_UnmappableLineItem_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            SeatItem("price_sponsorship", 1));

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_ItemDiscountsPresentButUnexpanded_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);

        // Construct the subscription via the same JSON path Stripe.NET uses on API responses: when
        // "discounts" is not in the request's Expand list, the SDK populates an item's Discounts
        // with a same-length list of null entries. Direct assignment of `[null]` is rewritten by
        // the SDK's expandable-field setter (see RedeemAnnualUpgradeOfferCommandTests for the same
        // workaround), so JSON deserialization is the only way to reproduce the unexpanded state
        // in a unit test.
        var unexpandedJson = $$"""
            {
              "id": "sub_123",
              "object": "subscription",
              "customer": "cus_123",
              "currency": "usd",
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_seat",
                    "object": "subscription_item",
                    "quantity": 5,
                    "price": { "id": "{{_currentPlan.PasswordManager.StripeSeatPlanId}}", "object": "price", "product": "prod_pm" },
                    "discounts": ["di_1"]
                  }
                ]
              }
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_AmountOffCouponMakesMonthlyCheaper_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        // A fixed amount comes off twelve monthly invoices but only one annual invoice, so a large
        // enough coupon genuinely leaves the organization better off staying monthly.
        subscription.Discounts =
        [
            new Discount
            {
                Id = "di_big",
                Coupon = new Coupon { Id = "big", AmountOff = 10_000, Duration = "forever", Currency = "usd" }
            }
        ];

        Assert.Null(await _query.Run(organization));
    }
}
