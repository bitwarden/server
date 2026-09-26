using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Commands;

using static StripeConstants;

public class RedeemAnnualUpgradeOfferCommandTests
{
    private static readonly DateTime _phase1Start = new(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _phase1End = new(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly IGetChurnOfferCohortMembershipQuery _getChurnOfferCohortMembershipQuery =
        Substitute.For<IGetChurnOfferCohortMembershipQuery>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<RedeemAnnualUpgradeOfferCommand> _logger =
        Substitute.For<ILogger<RedeemAnnualUpgradeOfferCommand>>();
    private readonly RedeemAnnualUpgradeOfferCommand _command;

    public RedeemAnnualUpgradeOfferCommandTests()
    {
        _getChurnOfferCohortMembershipQuery.Run(Arg.Any<Organization>()).Returns((ChurnOfferCohortMembership?)null);
        _command = new RedeemAnnualUpgradeOfferCommand(
            _logger,
            _getChurnOfferCohortMembershipQuery,
            _priceIncreaseScheduler,
            _pricingClient,
            _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    private (Subscription Subscription, SubscriptionSchedule Schedule) SetupRedeemableSubscription(
        Organization organization,
        List<SubscriptionItem> items,
        List<Discount>? subscriptionDiscounts = null,
        Customer? customer = null,
        List<SubscriptionSchedulePhaseDiscount>? phase1Discounts = null)
    {
        foreach (var item in items)
        {
            item.CurrentPeriodStart = _phase1Start;
            item.CurrentPeriodEnd = _phase1End;
        }

        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Customer = customer,
            Discounts = subscriptionDiscounts,
            Items = new StripeList<SubscriptionItem> { Data = items }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_new",
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = _phase1Start,
                    EndDate = _phase1End,
                    Items = [.. items.Select(i => new SubscriptionSchedulePhaseItem { PriceId = i.Price.Id, Quantity = i.Quantity })],
                    Discounts = phase1Discounts
                }
            ]
        };
        _stripeAdapter.CreateSubscriptionScheduleWithPhasesAsync(
                Arg.Any<Subscription>(), Arg.Any<SubscriptionSchedulePhaseOptions>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(schedule);

        return (subscription, schedule);
    }

    [Fact]
    public async Task Run_ChurnCohortMember_ReturnsOfferNoLongerAvailable_StripeNotMutated()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns(
            new ChurnOfferCohortMembership(
                new OrganizationPlanMigrationCohortAssignment { Id = Guid.NewGuid(), OrganizationId = organization.Id, CohortId = Guid.NewGuid() },
                new OrganizationPlanMigrationCohort { Id = Guid.NewGuid(), Name = "cohort", IsActive = true, ChurnDiscountCouponCode = "coupon" }));

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_RequestsNoInvoicePreviewAndExactlyOneSubscriptionRetrieve()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateInvoicePreviewAsync(default!);
        await _stripeAdapter.Received(1).GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_HappyPath_CreatesAnnualUpgradeScheduleWithOneYearPhase2()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).GetSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o =>
                o.Expand.Contains("discounts.source.coupon") &&
                o.Expand.Contains("items.data.discounts.source") &&
                o.Expand.Contains("schedule")));
        // Passing organization.Id (not null) is what drops the org's cohort assignment inside
        // ReleaseSchedule -- switching to annual also exits the cohort.
        await _priceIncreaseScheduler.Received(1).ReleaseSchedule(null, organization.Id);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Is(subscription),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 =>
                // Phase 2 starts at the subscription's current period end and runs exactly one annual term.
                phase2.StartDate == _phase1End &&
                phase2.EndDate == _phase1End.AddYears(1) &&
                phase2.Items.Count == 1 &&
                phase2.Items[0].Price == annualPlan.PasswordManager.StripeSeatPlanId &&
                phase2.Items[0].Quantity == 10 &&
                phase2.Discounts == null &&
                phase2.ProrationBehavior == ProrationBehavior.None &&
                phase2.Metadata == null),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_MixedLineItems_MapEachToTheirAnnualPrice()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupRedeemableSubscription(organization,
        [
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeStoragePlanId }, Quantity = 2 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.SecretsManager!.StripeSeatPlanId }, Quantity = 5 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.SecretsManager!.StripeServiceAccountPlanId }, Quantity = 3 }
        ]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 =>
                phase2.Items.Count == 4 &&
                phase2.Items.Any(i => i.Price == annualPlan.PasswordManager.StripeSeatPlanId && i.Quantity == 10) &&
                phase2.Items.Any(i => i.Price == annualPlan.PasswordManager.StripeStoragePlanId && i.Quantity == 2) &&
                phase2.Items.Any(i => i.Price == annualPlan.SecretsManager!.StripeSeatPlanId && i.Quantity == 5) &&
                phase2.Items.Any(i => i.Price == annualPlan.SecretsManager!.StripeServiceAccountPlanId && i.Quantity == 3)),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_UnmappableLineItem_ReturnsOfferNoLongerAvailable_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupRedeemableSubscription(organization,
        [
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 },
            new SubscriptionItem { Price = new Price { Id = "price_no_mapping" }, Quantity = 1 }
        ]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        // A redemption that cannot be fully mapped must fail before the org's existing
        // schedule and cohort assignment are destroyed.
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_SubscriptionWithDiscounts_CarriesThemByReuse_AndDoesNotAddCustomerCoupon()
    {
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var organization = CreateOrganization(PlanType.TeamsMonthly);
        SetupRedeemableSubscription(
            organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            subscriptionDiscounts: [new Discount { Id = "di_own", Source = new DiscountSource { CouponId = "promo-coupon" } }],
            customer: new Customer { Discount = new Discount { Id = "di_customer", Source = new DiscountSource { CouponId = "customer-coupon" } } });

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 =>
                phase2.Discounts != null &&
                phase2.Discounts.Count == 1 &&
                phase2.Discounts[0].Discount == "di_own" &&
                phase2.Discounts[0].Coupon == null &&
                phase2.Discounts.All(d => d.Coupon != "customer-coupon" && d.Discount != "di_customer")),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_OnlyCustomerCoupon_LeavesPhaseDiscountsNullSoStripeInherits()
    {
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var organization = CreateOrganization(PlanType.TeamsMonthly);
        SetupRedeemableSubscription(
            organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            customer: new Customer { Discount = new Discount { Id = "di_customer", Source = new DiscountSource { CouponId = "customer-coupon" } } });

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Discounts == null),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_UnusableDiscounts_ReturnsBadRequest_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);

        // Construct the subscription via the same JSON path Stripe.NET uses on API responses:
        // when "discounts" is not in the request's Expand list, the SDK populates Discounts with
        // a same-length list of null entries. Direct assignment of `[null]` is rewritten by the
        // SDK's expandable-field setter, so JSON deserialization is the only way to reproduce
        // the unexpanded state in a unit test.
        const string unexpandedJson = """
            {
              "id": "sub_123",
              "object": "subscription",
              "customer": "cus_123",
              "discounts": ["di_abc"]
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_UnexpandedItemDiscounts_ReturnsBadRequest_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);

        // Same JSON-deserialization approach as the subscription-level unexpanded-discounts test
        // above: when "items.data.discounts.coupon" is not in the request's Expand list,
        // Stripe.NET populates each item's Discounts with a same-length list of null entries.
        const string unexpandedJson = """
            {
              "id": "sub_123",
              "object": "subscription",
              "customer": "cus_123",
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_123",
                    "object": "subscription_item",
                    "discounts": ["di_line"]
                  }
                ]
              }
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_SubscriptionDiscountHasNullCoupon_ReturnsBadRequest_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // A discount with no coupon is just as unusable as an unexpanded one: it would silently
        // drop a subscription-level coupon, so redemption refuses instead.
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            subscriptionDiscounts: [new Discount { Source = new DiscountSource() }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_SubscriptionDiscountHasEmptyCouponId_ReturnsBadRequest_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // Same failure mode as the null case above, but with an empty coupon id.
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            subscriptionDiscounts: [new Discount { Source = new DiscountSource { CouponId = "" } }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_SubscriptionNotFound_ReturnsBadRequestAndLogsWarning()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_PlanTypeHasNoAnnualMapping_ReturnsConflict_WithoutFetchingSubscription()
    {
        // The org's plan type has no annual-latest mapping (e.g. it is already annual). The
        // command must fail closed with the default conflict before touching Stripe.
        var organization = CreateOrganization(PlanType.TeamsAnnually);

        var result = await _command.Run(organization);

        Assert.True(result.IsT2);
        Assert.Equal("We had a problem switching your billing to annual. Please contact support for assistance.", result.AsT2.Response);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Run_NoGatewaySubscriptionId_ReturnsOfferNoLongerAvailable_WithoutFetchingSubscription(string? gatewaySubscriptionId)
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        organization.GatewaySubscriptionId = gatewaySubscriptionId;

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_ForeignSchedule_ReturnsBadRequestWithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_negotiated",
            Status = SubscriptionScheduleStatus.Active,
            Phases = [new SubscriptionSchedulePhase { Metadata = new Dictionary<string, string>() }]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs().ReleaseSchedule(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_PersonalPriceIncreaseSchedule_ReturnsBadRequestWithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_personal",
            Status = SubscriptionScheduleStatus.Active,
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.ManagingSystem] = ManagingSystems.PersonalPriceIncrease
            },
            Phases = [new SubscriptionSchedulePhase()]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs().ReleaseSchedule(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_UnexpandedSchedule_ReturnsBadRequestWithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);
        subscription.ScheduleId = "sub_sched_unread";
        subscription.Schedule = null;

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs().ReleaseSchedule(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_PriceMigrationSchedule_ReleasesResolvedSchedule()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var monthlyPlan = new Teams2020Plan(false);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 1 }]);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_migration",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    Metadata = new Dictionary<string, string>
                    {
                        [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
                    }
                }
            ]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).ReleaseSchedule(schedule);
    }

    [Fact]
    public async Task Run_CreateScheduleThrows_PreservesCohortAssignment()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var monthlyPlan = new Teams2020Plan(false);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 1 }]);

        _stripeAdapter.CreateSubscriptionScheduleWithPhasesAsync(
                Arg.Any<Subscription>(), Arg.Any<SubscriptionSchedulePhaseOptions>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "api_error" } });

        var result = await _command.Run(organization);

        Assert.False(result.IsT0);
        // The pre-create release must not carry the organization id, so the cohort assignment survives.
        await _priceIncreaseScheduler.DidNotReceive().ReleaseSchedule(Arg.Any<SubscriptionSchedule?>(), organization.Id);
        Assert.True(result.IsT3);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("recovery scheduler will re-create the released schedule")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_ReturnsOfferNoLongerAvailable_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        var (subscription, _) = SetupRedeemableSubscription(organization, []);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_annual_upgrade",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    Metadata = new Dictionary<string, string>
                    {
                        [MetadataKeys.AnnualUpgrade] = PlanType.TeamsMonthly2020.ToString()
                    }
                }
            ]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs().ReleaseSchedule(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionScheduleWithPhasesAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Run_NoSchedule_StillDropsCohortAssignment()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var monthlyPlan = new Teams2020Plan(false);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 1 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).ReleaseSchedule(null, organization.Id);
    }

    [Fact]
    public async Task Run_NonActiveSchedule_ReleasesNothingButStillDropsCohortAssignment()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var monthlyPlan = new Teams2020Plan(false);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));
        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 1 }]);
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_canceled",
            Status = SubscriptionScheduleStatus.Canceled,
            Phases = []
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).ReleaseSchedule(null, organization.Id);
    }

    [Fact]
    public async Task Run_ItemLevelDiscounts_CopiedOntoPhaseTwoItems()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var seatItem = new SubscriptionItem
        {
            Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId },
            Quantity = 10,
            Discounts = [new Discount { Id = "di_1", Source = new DiscountSource { CouponId = "sm_half_off" } }]
        };
        SetupRedeemableSubscription(organization, [seatItem]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 =>
                phase2.Items.Any(item =>
                    item.Discounts != null &&
                    item.Discounts.Any(discount => discount.Coupon == "sm_half_off"))),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_ItemWithoutDiscounts_LeavesPhaseTwoItemDiscountsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Items.All(item => item.Discounts == null)),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_SubscriptionDiscountsEmpty_LeavesPhase2DiscountsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // An empty (but non-null) collection, not the null-source case the "?." already handles.
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            subscriptionDiscounts: []);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Discounts == null),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_ItemDiscountsEmpty_LeavesPhaseTwoItemDiscountsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // An empty (but non-null) collection on the item itself, distinct from
        // Run_ItemWithoutDiscounts_LeavesPhaseTwoItemDiscountsNull above, which never sets
        // Discounts at all and so only exercises the null-source case.
        var seatItem = new SubscriptionItem
        {
            Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId },
            Quantity = 10,
            Discounts = []
        };
        SetupRedeemableSubscription(organization, [seatItem]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Items.All(item => item.Discounts == null)),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_CreatesScheduleManagedByAnnualUpgrade_WithoutPhaseMetadata()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, _) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 5 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Is(subscription),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Metadata == null),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

    [Fact]
    public async Task Run_NoCouponsAtEitherLevel_Phase2DiscountsAreNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            customer: new Customer { Id = "cus_123" });

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleWithPhasesAsync(
            Arg.Any<Subscription>(),
            Arg.Is<SubscriptionSchedulePhaseOptions>(phase2 => phase2.Discounts == null),
            Arg.Is(ManagingSystems.AnnualUpgrade),
            Arg.Is<Dictionary<string, string>>(metadata => metadata == null));
    }

}
