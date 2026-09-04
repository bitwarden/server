using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

public class AnnualUpgradeLineMapperTests
{
    private readonly ILogger<AnnualUpgradeLineMapperTests> _logger =
        Substitute.For<ILogger<AnnualUpgradeLineMapperTests>>();

    private IReadOnlyList<AnnualUpgradeLine>? Map(Subscription subscription) =>
        AnnualUpgradeLineMapper.MapOrNull(
            _logger, Guid.NewGuid(), subscription, MonthlyTeamsPlan(), AnnualTeamsPlan());

    private void AssertLogged(LogLevel level, string expectedContent) =>
        _logger.Received(1).Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(expectedContent)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    private void AssertNothingLogged() =>
        _logger.DidNotReceiveWithAnyArgs().Log<object>(default, default, default!, default, default!);

    private static Plan MonthlyTeamsPlan() => new Teams2023Plan(isAnnual: false);
    private static Plan AnnualTeamsPlan() => new Teams2023Plan(isAnnual: true);

    private static Subscription SubscriptionWith(
        IEnumerable<SubscriptionItem>? items = null,
        SubscriptionSchedule? schedule = null,
        List<Discount>? discounts = null) => new()
        {
            Id = "sub_1",
            ScheduleId = schedule?.Id,
            Schedule = schedule,
            Discounts = discounts,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [.. items ?? [Item("2023-teams-org-seat-monthly")]]
            }
        };

    private static SubscriptionItem Item(string priceId, List<Discount>? discounts = null) => new()
    {
        Id = $"si_{priceId}",
        Price = new Price { Id = priceId },
        Quantity = 5,
        Discounts = discounts
    };

    private static SubscriptionSchedule Schedule(Dictionary<string, string>? phaseMetadata) => new()
    {
        Id = "sub_sched_1",
        Status = StripeConstants.SubscriptionScheduleStatus.Active,
        Phases = [new SubscriptionSchedulePhase { Metadata = phaseMetadata }]
    };

    private static Subscription UnexpandedDiscountSubscription()
    {
        // Construct the subscription via the same JSON path Stripe.NET uses on API responses: when
        // "discounts" is not in the request's Expand list, the SDK populates Discounts with a
        // same-length list of null entries. Direct assignment of `[null]` is rewritten by the SDK's
        // expandable-field setter, so JSON deserialization is the only way to reproduce the
        // unexpanded state in a unit test.
        const string unexpandedJson = """
            {
              "id": "sub_1",
              "object": "subscription",
              "discounts": ["di_abc"],
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_2023-teams-org-seat-monthly",
                    "object": "subscription_item",
                    "quantity": 5,
                    "price": { "id": "2023-teams-org-seat-monthly", "object": "price" }
                  }
                ]
              }
            }
            """;
        return Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;
    }

    [Fact]
    public void MapOrNull_MappableLines_PairsEachLineWithItsTarget()
    {
        var result = Map(SubscriptionWith());

        var line = Assert.Single(result!);
        Assert.Equal("2023-teams-org-seat-monthly", line.Item.Price.Id);
        Assert.Equal(AnnualTeamsPlan().PasswordManager.StripeSeatPlanId, line.TargetPriceId);
        AssertNothingLogged();
    }

    [Fact]
    public void MapOrNull_UnmappableLine_ReturnsNullAndLogsTheOffendingPriceId()
    {
        Assert.Null(Map(SubscriptionWith(items: [Item("some-unmapped-price")])));

        AssertLogged(LogLevel.Warning, "some-unmapped-price");
    }

    [Fact]
    public void MapOrNull_NoPricedLines_ReturnsNullAndLogsNoMapping()
    {
        Assert.Null(Map(SubscriptionWith(items: [new SubscriptionItem { Id = "si_1", Price = null }])));

        AssertLogged(LogLevel.Warning, "has no annual-latest mapping");
    }

    [Fact]
    public void MapOrNull_LineWithNoPriceObject_IsRefusedRatherThanSkipped()
    {
        Assert.Null(Map(SubscriptionWith(
            items: [new SubscriptionItem { Id = "si_0", Price = null }, Item("2023-teams-org-seat-monthly")])));

        AssertLogged(LogLevel.Warning, "has no annual-latest mapping");
    }

    [Fact]
    public void MapOrNull_NoLineItemsAtAll_ReturnsNullAndLogsNoLineItems()
    {
        Assert.Null(Map(SubscriptionWith(items: [])));

        AssertLogged(LogLevel.Warning, "has no line items to map");
    }

    [Fact]
    public void MapOrNull_NullDiscountEntry_ReturnsNullAndLogsUnusableDiscount()
    {
        Assert.Null(Map(UnexpandedDiscountSubscription()));

        AssertLogged(LogLevel.Error, "unexpanded or couponless discount");
    }

    [Fact]
    public void MapOrNull_DiscountWithNoCouponId_ReturnsNullAndLogsUnusableDiscount()
    {
        // Stricter than the page-load path was before: a discount with no usable coupon id is as
        // unquotable as an unexpanded one.
        Assert.Null(Map(SubscriptionWith(discounts: [new Discount { Source = new DiscountSource() }])));

        AssertLogged(LogLevel.Error, "unexpanded or couponless discount");
    }

    [Fact]
    public void MapOrNull_CouponSourcedDiscounts_AreNotUnusable()
    {
        var couponDiscount = new Discount { Source = new DiscountSource { CouponId = "coupon_1" } };

        var result = Map(SubscriptionWith(
            items: [Item("2023-teams-org-seat-monthly", discounts: [couponDiscount])],
            discounts: [couponDiscount]));

        Assert.NotNull(result);
        AssertNothingLogged();
    }

    [Fact]
    public void MapOrNull_NullItemDiscountEntry_ReturnsNullAndLogsUnusableDiscount()
    {
        // Same JSON-deserialization workaround as UnexpandedDiscountSubscription() above.
        const string unexpandedJson = """
            {
              "id": "sub_1",
              "object": "subscription",
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_2023-teams-org-seat-monthly",
                    "object": "subscription_item",
                    "quantity": 5,
                    "price": { "id": "2023-teams-org-seat-monthly", "object": "price" },
                    "discounts": ["di_1"]
                  }
                ]
              }
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;

        Assert.Null(Map(subscription));

        AssertLogged(LogLevel.Error, "unexpanded or couponless discount");
    }

    [Fact]
    public void MapOrNull_UnexpandedSchedule_ReturnsNullAndLogsError()
    {
        var subscription = SubscriptionWith();
        subscription.ScheduleId = "sub_sched_unread";
        subscription.Schedule = null;

        Assert.Null(Map(subscription));

        AssertLogged(LogLevel.Error, "was not expanded");
    }

    [Fact]
    public void MapOrNull_AnnualUpgradeMarker_ReturnsNullAndLogsInformation()
    {
        var schedule = Schedule(new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.AnnualUpgrade] = "TeamsMonthly"
        });

        Assert.Null(Map(SubscriptionWith(schedule: schedule)));

        AssertLogged(LogLevel.Information, "already redeemed the annual upgrade offer");
    }

    [Fact]
    public void MapOrNull_UnrecognizedScheduleMetadata_ReturnsNullAndLogsWarning()
    {
        Assert.Null(Map(SubscriptionWith(
            schedule: Schedule(new Dictionary<string, string> { ["negotiated_term"] = "3y" }))));

        AssertLogged(LogLevel.Warning, "unrecognized schedule");
    }

    [Fact]
    public void MapOrNull_MigrationCohortSchedule_ProceedsToMapping()
    {
        var schedule = Schedule(new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
        });

        Assert.NotNull(Map(SubscriptionWith(schedule: schedule)));
        AssertNothingLogged();
    }

    [Fact]
    public void MapOrNull_NoScheduleAttached_ProceedsToMapping()
    {
        Assert.NotNull(Map(SubscriptionWith()));
        AssertNothingLogged();
    }

    [Fact]
    public void MapOrNull_ForeignScheduleAndUnusableDiscount_ReportsTheDiscount()
    {
        var subscription = UnexpandedDiscountSubscription();
        var schedule = Schedule(new Dictionary<string, string> { ["negotiated_term"] = "3y" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        Assert.Null(Map(subscription));

        AssertLogged(LogLevel.Error, "unexpanded or couponless discount");
    }

    [Fact]
    public void MapOrNull_ForeignScheduleAndUnmappableLine_ReportsTheSchedule()
    {
        // Ownership is checked before line mapping, so the schedule is what gets reported.
        var subscription = SubscriptionWith(
            items: [Item("some-unmapped-price")],
            schedule: Schedule(new Dictionary<string, string> { ["negotiated_term"] = "3y" }));

        Assert.Null(Map(subscription));

        AssertLogged(LogLevel.Warning, "unrecognized schedule");
    }
}
