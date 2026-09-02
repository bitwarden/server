using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetPendingAnnualUpgradeQueryTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<GetPendingAnnualUpgradeQuery> _logger =
        Substitute.For<ILogger<GetPendingAnnualUpgradeQuery>>();
    private readonly GetPendingAnnualUpgradeQuery _query;

    public GetPendingAnnualUpgradeQueryTests()
    {
        _query = new GetPendingAnnualUpgradeQuery(
            _logger, _pricingClient, _stripeAdapter);
    }

    private static Organization CreateOrganization(PlanType planType) => new()
    {
        Id = Guid.NewGuid(),
        PlanType = planType,
        GatewaySubscriptionId = "sub_123"
    };

    private Subscription SetupSubscription(Organization organization)
    {
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        return subscription;
    }

    // The caller controls the future phase's metadata, since that is what ownership classification reads.
    private SubscriptionSchedule ScheduleWithUpcomingAnnualPhase(Dictionary<string, string>? metadata)
    {
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;

        return new SubscriptionSchedule
        {
            Id = "sub_sched_1",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_monthly", Quantity = 5 }]
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(1),
                    Metadata = metadata,
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            PriceId = annualSeatPriceId,
                            Price = new Price
                            {
                                Nickname = "Teams (Annually) Seat",
                                UnitAmount = 4800,
                                UnitAmountDecimal = 4800,
                                ProductId = "prod_teams",
                                Recurring = new PriceRecurring { Interval = "year" }
                            },
                            Quantity = 5
                        }
                    ]
                }
            ]
        };
    }

    private Subscription AttachSchedule(Organization organization, SubscriptionSchedule schedule)
    {
        schedule.Id ??= "sub_sched_1";
        var subscription = SetupSubscription(organization);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;
        return subscription;
    }

    [Fact]
    public async Task Run_ScheduleWithAnnualPriceButNoMetadata_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var schedule = ScheduleWithUpcomingAnnualPhase(metadata: null);
        AttachSchedule(organization, schedule);

        Assert.Null(await _query.Run(organization));
    }

    [Fact]
    public async Task Run_ScheduleWithAnnualUpgradeMetadata_ReturnsPendingUpgrade()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var schedule = ScheduleWithUpcomingAnnualPhase(
            metadata: new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" });
        AttachSchedule(organization, schedule);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(schedule.Phases[^1].StartDate, result.EffectiveDate);
    }

    [Fact]
    public async Task Run_AnnualPlanType_ReturnsNull()
    {
        var result = await _query.Run(CreateOrganization(PlanType.TeamsAnnually));

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_SubscriptionNotActive_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        // Otherwise-valid redeemed schedule, so Status is the only thing that can produce null.
        var schedule = ScheduleWithUpcomingAnnualPhase(
            metadata: new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" });
        var subscription = AttachSchedule(organization, schedule);
        subscription.Status = SubscriptionStatus.PastDue;

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_NoScheduleAttached_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        SetupSubscription(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TeamsPlan(true));

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ActiveScheduleHasNoUpcomingPhase_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;

        // Only a past-dated phase is present -- the schedule is "active" and already targets the
        // annual-latest seat price, but there is no future phase left to report as pending.
        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" },
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = annualSeatPriceId, Quantity = 5 }]
                }
            ]
        };
        AttachSchedule(organization, schedule);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_RedeemedSchedule_ReturnsTargetPlanAndLineItems()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;
        var renewalDate = DateTime.UtcNow.AddMonths(1);

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" },
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_monthly", Quantity = 5 }]
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = renewalDate,
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            PriceId = annualSeatPriceId,
                            Price = new Price
                            {
                                Nickname = "Teams (Annually) Seat",
                                UnitAmount = 4800,
                                UnitAmountDecimal = 4800,
                                ProductId = "prod_teams",
                                Recurring = new PriceRecurring { Interval = "year" }
                            },
                            Quantity = 5
                        }
                    ]
                }
            ]
        };
        AttachSchedule(organization, schedule);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(annualPlan.Type, result.Plan.Type);
        Assert.Equal(renewalDate, result.EffectiveDate);
        var lineItem = Assert.Single(result.LineItems);
        Assert.Equal("Teams (Annually) Seat", lineItem.Name);
        Assert.Equal(48m, lineItem.Amount);
        Assert.Equal(5, lineItem.Quantity);
        Assert.Equal("year", lineItem.Interval);
    }

    [Fact]
    public async Task Run_RequestsExpandedSchedulePhasePrices()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        SetupSubscription(organization);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);

        await _query.Run(organization);

        await _stripeAdapter.Received().GetSubscriptionAsync(
            organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o =>
                o.Expand != null && o.Expand.Contains("schedule.phases.items.price")));
    }

    [Fact]
    public async Task Run_UpcomingPhaseLacksAnnualSeatPrice_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;
        var renewalDate = DateTime.UtcNow.AddMonths(1);

        // Schedule is redeemed (AnnualUpgrade metadata present), but the earliest FUTURE phase is
        // a different (non-annual) price -> content check must reject it.
        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" },
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = annualSeatPriceId, Quantity = 5 }]
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = renewalDate,
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_other", Quantity = 5 }]
                }
            ]
        };
        AttachSchedule(organization, schedule);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_UnexpandedSchedule_ReturnsNullAndLogsError()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var subscription = SetupSubscription(organization);
        subscription.ScheduleId = "sub_sched_unread";
        subscription.Schedule = null;

        var result = await _query.Run(organization);

        Assert.Null(result);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_PricingLookupThrows_ReturnsNullAndLogsWarning()
    {
        // Schedule attached so the pricing lookup is reached.
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var schedule = ScheduleWithUpcomingAnnualPhase(
            metadata: new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" });
        AttachSchedule(organization, schedule);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually)
            .Throws(new NotFoundException("no plan"));

        var result = await _query.Run(organization);

        Assert.Null(result);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_NoGatewaySubscriptionId_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        organization.GatewaySubscriptionId = null;

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_SubscriptionMissing_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_SubscriptionRetrieveThrowsNonMissingStripeError_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "rate_limit" } });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    // Pins the widened catch clause: the old filter only covered StripeException, BillingException,
    // and NotFoundException, so a future narrowing back to those types must fail this test.
    [Fact]
    public async Task Run_GuardedRegionThrowsNonStripeException_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly2020);
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_AttachedScheduleNotActive_ReturnsNull()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Canceled,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(1),
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" },
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = annualSeatPriceId, Quantity = 5 }]
                }
            ]
        };
        AttachSchedule(organization, schedule);

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_UsesTestClockTime_AndFlagsAddonLineItems()
    {
        var organization = CreateOrganization(PlanType.TeamsMonthly);
        var annualPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(annualPlan);
        var annualSeatPriceId = annualPlan.PasswordManager.StripeSeatPlanId;
        // A test-clock-backed subscription: "now" comes from the frozen clock, not wall-clock time.
        var frozenNow = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var renewalDate = frozenNow.AddMonths(1);

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = renewalDate,
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" },
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            PriceId = annualSeatPriceId,
                            Price = new Price
                            {
                                Nickname = "Teams (Annually) Seat",
                                UnitAmount = 4800,
                                UnitAmountDecimal = 4800,
                                ProductId = "prod_teams",
                                Recurring = new PriceRecurring { Interval = "year" }
                            },
                            Quantity = 5
                        },
                        new SubscriptionSchedulePhaseItem
                        {
                            PriceId = "price_addon",
                            Price = new Price
                            {
                                Nickname = "Premium add-on",
                                UnitAmount = 1000,
                                UnitAmountDecimal = 1000,
                                ProductId = "prod_addon",
                                Recurring = new PriceRecurring { Interval = "year" },
                                Metadata = new Dictionary<string, string> { ["isAddOn"] = "true" }
                            },
                            Quantity = 1
                        }
                    ]
                }
            ]
        };
        var subscription = AttachSchedule(organization, schedule);
        subscription.TestClock = new Stripe.TestHelpers.TestClock { FrozenTime = frozenNow };

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(renewalDate, result.EffectiveDate);
        var addonLineItem = Assert.Single(result.LineItems.Where(lineItem => lineItem.AddonSubscriptionItem));
        Assert.Equal("Premium add-on", addonLineItem.Name);
        var seatLineItem = Assert.Single(result.LineItems.Where(lineItem => !lineItem.AddonSubscriptionItem));
        Assert.Equal("Teams (Annually) Seat", seatLineItem.Name);
    }
}
