using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetAnnualUpgradeOfferQueryTests
{
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
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(_currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(_annualLatestPlan);
        _query = new GetAnnualUpgradeOfferQuery(
            _logger, _getChurnOfferCohortMembershipQuery,
            _pricingClient, _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    // Populated by SetupSubscription with the price IDs actually on the subscription, so
    // SetupPreviews can tell the monthly-side preview call from the annual-side one without
    // assuming which plan a given test builds its line items from.
    private readonly HashSet<string> _subscriptionPriceIds = [];

    private Subscription SetupSubscription(Organization organization, params SubscriptionItem[] items)
    {
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Currency = "usd",
            Items = new StripeList<SubscriptionItem> { Data = [.. items] }
        };
        _subscriptionPriceIds.Clear();
        foreach (var priceId in items.Select(item => item.Price?.Id).OfType<string>())
        {
            _subscriptionPriceIds.Add(priceId);
        }
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

    // Stripe prices both sides now, so tests state the totals it returns rather than deriving them
    // from the plan catalog. Minor units, matching the API. The monthly side is priced on the
    // subscription's own price IDs unchanged, so a preview request is the monthly one exactly when
    // every item on it matches a price already on the subscription.
    private void SetupPreviews(long monthlyTotal, long annualTotal)
    {
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(callInfo =>
            {
                var options = callInfo.Arg<InvoiceCreatePreviewOptions>();
                var isMonthly = options.SubscriptionDetails.Items
                    .All(item => _subscriptionPriceIds.Contains(item.Price));
                return Task.FromResult(new Invoice
                {
                    Total = isMonthly ? monthlyTotal : annualTotal
                });
            });
    }

    // Opt-in variant of SetupPreviews for tests that need the coupon on the subscription to
    // actually influence the totals Stripe hands back, rather than the fixed totals above, which
    // ignore whatever the calculator puts on InvoiceCreatePreviewOptions.Discounts entirely. Looks
    // the discount up by coupon ID against the subscription's own discounts and subtracts its fixed
    // amount once per invoice, mirroring real Stripe behaviour where a fixed amount comes off one
    // invoice regardless of billing interval.
    private void SetupPreviewsWithDiscounts(Subscription subscription, long monthlyTotal, long annualTotal)
    {
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(callInfo =>
            {
                var options = callInfo.Arg<InvoiceCreatePreviewOptions>();
                var isMonthly = options.SubscriptionDetails.Items
                    .All(item => _subscriptionPriceIds.Contains(item.Price));
                var total = isMonthly ? monthlyTotal : annualTotal;

                var amountOff = (options.Discounts ?? [])
                    .Sum(discount => subscription.Discounts?
                        .FirstOrDefault(d => d.Source?.Coupon?.Id == discount.Coupon)
                        ?.Source?.Coupon?.AmountOff ?? 0);

                return Task.FromResult(new Invoice
                {
                    Total = total - amountOff
                });
            });
    }

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
        SetupPreviews(monthlyTotal: 8_000, annualTotal: 72_000);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(960m, result.CurrentAnnualCost);
        Assert.Equal(720m, result.NewAnnualCost);
        Assert.Equal(240m, result.Savings);
    }

    [Fact]
    public async Task Run_LegacyVintageMonthlyOrg_ComparesAgainstAnnualLatest()
    {
        // An org still on a legacy monthly vintage (e.g. pending a Track A price migration) has
        // savings computed against the annual-latest plan -- the same target the migration program
        // would move it to -- not the legacy-vintage annual plan. The previews come back equal, so
        // there are no positive savings and no offer is returned.
        var organization = CreateOrganization(PlanType.EnterpriseMonthly2020);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new Enterprise2020Plan(false);
        var annualLatestPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualLatestPlan);

        SetupSubscription(organization,
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 5 });
        // Annualizing the monthly preview (x12) lands on the same figure as the annual preview, so
        // the comparison nets to zero and no offer is returned.
        SetupPreviews(monthlyTotal: 3_000, annualTotal: 36_000);

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
        // Read path (runs inline on the organization subscription page): a missing subscription is
        // a data condition, not an operational failure, so it logs at Warning, not Error.
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
    public async Task Run_ItemWithNullPrice_IsUnmappableRatherThanSkipped_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _getChurnOfferCohortMembershipQuery.Run(organization).Returns((ChurnOfferCohortMembership?)null);

        var monthlyPlan = new TeamsPlan(false);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(monthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        // A line item with no expanded Price now fails the whole subscription rather than being skipped.
        SetupSubscription(organization,
            new SubscriptionItem { Price = null, Quantity = 1 },
            new SubscriptionItem { Price = new Price { Id = monthlyPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 });

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateInvoicePreviewAsync(default!);
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
    public async Task Run_ForeignSchedule_LogsWarning()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_negotiated", new Dictionary<string, string>
        {
            ["negotiated_term"] = "3y"
        });

        Assert.Null(await _query.Run(organization));

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("negotiated_term")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
    public async Task Run_AnnualUpgradeSchedule_LogsInformation()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_annual", new Dictionary<string, string>
        {
            [MetadataKeys.AnnualUpgrade] = nameof(PlanType.TeamsMonthly2020)
        });

        Assert.Null(await _query.Run(organization));

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
        SetupPreviews(monthlyTotal: 3_000, annualTotal: 30_000);

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
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
                options.Expand.Contains("customer.discount.source.coupon") &&
                options.Expand.Contains("discounts.source.coupon") &&
                options.Expand.Contains("items.data.discounts.source") &&
                !options.Expand.Any(expansion => expansion.EndsWith("applies_to"))));
    }

    [Fact]
    public async Task Run_SecretsManagerLines_IncludedInBothFigures()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            SeatItem(_currentPlan.SecretsManager.StripeSeatPlanId, 3));
        SetupPreviews(monthlyTotal: 5_000, annualTotal: 40_000);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(600m, result!.CurrentAnnualCost);

        // Both figures come from one preview call each; the SM line rides along on whichever side
        // it belongs to rather than needing separate handling.
        await _stripeAdapter.Received(2).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options => options.SubscriptionDetails.Items.Count == 2));
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
    public async Task Run_UnmappableLineItem_LogsWarning()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            SeatItem("price_sponsorship", 1));

        Assert.Null(await _query.Run(organization));

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("price_sponsorship")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
    public async Task Run_ItemDiscountsPresentButUnexpanded_LogsError()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);

        // Same JSON-deserialization workaround as the test above.
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

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
                Source = new DiscountSource
                {
                    CouponId = "big",
                    Coupon = new Coupon { Id = "big", AmountOff = 10_000, Duration = "forever", Currency = "usd" }
                }
            }
        ];
        // Pre-discount, annual is comfortably cheaper (1800 vs. 1200 annualized); the $100 coupon
        // coming off every one of twelve monthly invoices, but only once off the annual invoice, is
        // what flips the comparison and proves the coupon reached Stripe.
        SetupPreviewsWithDiscounts(subscription, monthlyTotal: 15_000, annualTotal: 120_000);

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_IssuesTwoPreviews_MonthlyOnItsOwnPricesAndAnnualOnTheMappedOnes()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        SetupPreviews(monthlyTotal: 2_000, annualTotal: 18_000);

        await _query.Run(organization);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price ==
                    _currentPlan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 5 &&
                options.AutomaticTax.Enabled == false));

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price ==
                    _annualLatestPlan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 5 &&
                options.AutomaticTax.Enabled == false));
    }

    [Fact]
    public async Task Run_PreviewThrows_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns<Invoice>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = ErrorCodes.InvoiceUpcomingNone }
            });

        Assert.Null(await _query.Run(organization));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_UnmappableLineItem_SuppressesBeforeAnyPreview()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            SeatItem("price_sponsorship", 1));

        Assert.Null(await _query.Run(organization));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateInvoicePreviewAsync(default!);
    }

    [Fact]
    public async Task Run_ForeignSchedule_SuppressesBeforeAnyPreview()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        var subscription = SetupSubscription(organization,
            SeatItem(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        AttachSchedule(subscription, "sub_sched_negotiated", new Dictionary<string, string>
        {
            ["negotiated_term"] = "3y"
        });

        Assert.Null(await _query.Run(organization));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateInvoicePreviewAsync(default!);
    }
}
