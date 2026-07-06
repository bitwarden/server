using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Commands;

using static StripeConstants;

public class RedeemAnnualUpgradeOfferCommandTests
{
    private static readonly DateTime _phase1Start = new(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _phase1End = new(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly IGetAnnualUpgradeOfferQuery _getOfferQuery = Substitute.For<IGetAnnualUpgradeOfferQuery>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly RedeemAnnualUpgradeOfferCommand _command;

    public RedeemAnnualUpgradeOfferCommandTests()
    {
        _command = new RedeemAnnualUpgradeOfferCommand(
            Substitute.For<ILogger<RedeemAnnualUpgradeOfferCommand>>(),
            _getOfferQuery,
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
        _getOfferQuery.Run(organization).Returns(new AnnualUpgradeOfferResult(60m, 48m, 12m));

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
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>()).Returns(schedule);

        return (subscription, schedule);
    }

    [Fact]
    public async Task Run_QueryReturnsNullOnRevalidation_ReturnsFailure_StripeNotMutated()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getOfferQuery.Run(organization).Returns((AnnualUpgradeOfferResult?)null);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        Assert.Equal("Offer is no longer available.", result.AsT1.Response);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Run_HappyPath_BuildsBoundedTwoPhaseSchedule_PreservingPhase1()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (subscription, schedule) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).GetSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("customer") && o.Expand.Contains("discounts.coupon")));
        // Passing organization.Id (not null) is what drops the org's cohort assignment inside
        // Release -- Alex's 2026-07-02 decision that switching to annual also exits the cohort.
        await _priceIncreaseScheduler.Received(1).Release(subscription.CustomerId, subscription.Id, organization.Id);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
            o.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
            o.Phases.Count == 2 &&
            // Phase 1 round-trips unchanged.
            o.Phases[0].StartDate == _phase1Start &&
            o.Phases[0].EndDate == _phase1End &&
            o.Phases[0].Items.Count == 1 &&
            o.Phases[0].Items[0].Price == monthlyPlan.PasswordManager.StripeSeatPlanId &&
            o.Phases[0].Items[0].Quantity == 10 &&
            o.Phases[0].Discounts == null &&
            o.Phases[0].ProrationBehavior == ProrationBehavior.None &&
            // Phase 2 is bounded to exactly one annual term.
            o.Phases[1].StartDate == _phase1End &&
            o.Phases[1].EndDate == _phase1End.AddYears(1) &&
            o.Phases[1].Items.Count == 1 &&
            o.Phases[1].Items[0].Price == annualPlan.PasswordManager.StripeSeatPlanId &&
            o.Phases[1].Items[0].Quantity == 10 &&
            o.Phases[1].Discounts == null &&
            o.Phases[1].ProrationBehavior == ProrationBehavior.None));
    }

    [Fact]
    public async Task Run_MixedLineItems_MapEachToTheirAnnualPrice()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (_, schedule) = SetupRedeemableSubscription(organization,
        [
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeStoragePlanId }, Quantity = 2 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.SecretsManager!.StripeSeatPlanId }, Quantity = 5 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.SecretsManager!.StripeServiceAccountPlanId }, Quantity = 3 }
        ]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
            o.Phases.Count == 2 &&
            o.Phases[1].Items.Count == 4 &&
            o.Phases[1].Items.Any(i => i.Price == annualPlan.PasswordManager.StripeSeatPlanId && i.Quantity == 10) &&
            o.Phases[1].Items.Any(i => i.Price == annualPlan.PasswordManager.StripeStoragePlanId && i.Quantity == 2) &&
            o.Phases[1].Items.Any(i => i.Price == annualPlan.SecretsManager!.StripeSeatPlanId && i.Quantity == 5) &&
            o.Phases[1].Items.Any(i => i.Price == annualPlan.SecretsManager!.StripeServiceAccountPlanId && i.Quantity == 3)));
    }

    [Fact]
    public async Task Run_UnmappableLineItem_ReturnsConflict_WithoutMutatingStripe()
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

        Assert.True(result.IsT2);
        Assert.Equal("We had a problem switching your billing to annual. Please contact support for assistance.", result.AsT2.Response);
        // A redemption that cannot be fully mapped must fail before the org's existing
        // schedule and cohort assignment are destroyed.
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Run_SubscriptionWithDiscounts_PreservesPhase1_AndMergesIntoPhase2()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        var (_, schedule) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }],
            subscriptionDiscounts: [new Discount { Coupon = new Coupon { Id = "promo-coupon" } }],
            customer: new Customer { Discount = new Discount { Coupon = new Coupon { Id = "customer-coupon" } } },
            phase1Discounts: [new SubscriptionSchedulePhaseDiscount { CouponId = "promo-coupon" }]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
            o.Phases[0].Discounts != null &&
            o.Phases[0].Discounts.Count == 1 &&
            o.Phases[0].Discounts[0].Coupon == "promo-coupon" &&
            // Customer coupon first, then existing subscription coupons, de-duplicated.
            o.Phases[1].Discounts != null &&
            o.Phases[1].Discounts.Count == 2 &&
            o.Phases[1].Discounts[0].Coupon == "customer-coupon" &&
            o.Phases[1].Discounts[1].Coupon == "promo-coupon"));
    }

    [Fact]
    public async Task Run_UnexpandedDiscounts_ReturnsConflict_WithoutMutatingStripe()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getOfferQuery.Run(organization).Returns(new AnnualUpgradeOfferResult(60m, 48m, 12m));

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

        Assert.True(result.IsT2);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Run_SubscriptionNotFound_ReturnsConflict()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getOfferQuery.Run(organization).Returns(new AnnualUpgradeOfferResult(60m, 48m, 12m));
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _command.Run(organization);

        Assert.True(result.IsT2);
        await _priceIncreaseScheduler.DidNotReceive().Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Run_ScheduleUpdateFails_ReleasesOrphanedScheduleAndReturnsUnhandled()
    {
        var organization = CreateOrganization(PlanType.EnterpriseMonthly);
        var monthlyPlan = new EnterprisePlan(false);
        var annualPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var (_, schedule) = SetupRedeemableSubscription(organization,
            [new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 5 }]);
        _stripeAdapter.UpdateSubscriptionScheduleAsync(schedule.Id, Arg.Any<SubscriptionScheduleUpdateOptions>())
            .Returns<SubscriptionSchedule>(_ => throw new StripeException { StripeError = new StripeError { Code = "api_error" } });

        // BaseBillingCommand.HandleAsync catches any StripeException not in ErrorCodes.InputErrors()
        // and returns it boxed as an Unhandled (T3) result rather than letting it propagate to the
        // caller -- see BaseBillingCommand.cs's final `catch (StripeException stripeException)` block.
        // So the command never throws here; it returns a non-success result instead. The two
        // behavioral guarantees under test are: the orphaned schedule gets released exactly once,
        // and the command does not report success.
        var result = await _command.Run(organization);

        Assert.False(result.IsT0);
        Assert.True(result.IsT3);
        Assert.IsType<StripeException>(result.AsT3.Exception);

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync(schedule.Id);
    }
}
