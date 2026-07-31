using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

public class AnnualUpgradeEligibilityMapperTests
{
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
    public void Map_MappableLines_IsEligibleAndPairsEachLineWithItsTarget()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.True(result.IsEligible);
        Assert.Null(result.Reason);
        Assert.Null(result.UnmappablePriceId);
        var line = Assert.Single(result.Lines);
        Assert.Equal("2023-teams-org-seat-monthly", line.Item.Price.Id);
        Assert.Equal(AnnualTeamsPlan().PasswordManager.StripeSeatPlanId, line.TargetPriceId);
    }

    [Fact]
    public void Map_UnmappableLine_ReportsTheOffendingPriceId()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(items: [Item("some-unmapped-price")]), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.False(result.IsEligible);
        Assert.Equal(AnnualUpgradeIneligibleReason.UnmappableLine, result.Reason);
        Assert.Equal("some-unmapped-price", result.UnmappablePriceId);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Map_NoPricedLines_IsUnmappableWithNoPriceId()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(items: [new SubscriptionItem { Id = "si_1", Price = null }]),
            MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.UnmappableLine, result.Reason);
        Assert.Null(result.UnmappablePriceId);
    }

    [Fact]
    public void Map_LineWithNoPriceObject_IsSkippedRatherThanFailing()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(items: [new SubscriptionItem { Id = "si_0", Price = null }, Item("2023-teams-org-seat-monthly")]),
            MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.True(result.IsEligible);
        Assert.Single(result.Lines);
    }

    [Fact]
    public void Map_NullDiscountEntry_IsUnexpandedDiscounts()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            UnexpandedDiscountSubscription(), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.UnexpandedDiscounts, result.Reason);
    }

    [Fact]
    public void Map_DiscountWithNoCouponId_IsUnexpandedDiscounts()
    {
        // Stricter than the page-load path was before: a discount with no usable coupon id is as
        // unquotable as an unexpanded one.
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(discounts: [new Discount { Coupon = null }]), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.UnexpandedDiscounts, result.Reason);
    }

    [Fact]
    public void Map_NullItemDiscountEntry_IsUnexpandedDiscounts()
    {
        // Same JSON-deserialization approach as the subscription-level unexpanded-discounts fixture
        // above: when an item's "discounts" is not in the request's Expand list, Stripe.NET
        // populates the item's Discounts with a same-length list of null entries. Direct assignment
        // of `[null]` is rewritten by the SDK's expandable-field setter, so JSON deserialization is
        // the only way to reproduce the unexpanded state in a unit test.
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

        var result = AnnualUpgradeEligibilityMapper.Map(subscription, MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.UnexpandedDiscounts, result.Reason);
    }

    [Fact]
    public void Map_UnexpandedSchedule_IsUnexpandedSchedule()
    {
        var subscription = SubscriptionWith();
        subscription.ScheduleId = "sub_sched_unread";
        subscription.Schedule = null;

        var result = AnnualUpgradeEligibilityMapper.Map(subscription, MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.UnexpandedSchedule, result.Reason);
    }

    [Fact]
    public void Map_AnnualUpgradeMarker_IsAlreadyScheduled()
    {
        var schedule = Schedule(new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.AnnualUpgrade] = "TeamsMonthly"
        });

        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(schedule: schedule), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.AlreadyScheduled, result.Reason);
    }

    [Fact]
    public void Map_UnrecognizedScheduleMetadata_IsForeignSchedule()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(schedule: Schedule(new Dictionary<string, string> { ["negotiated_term"] = "3y" })),
            MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.Equal(AnnualUpgradeIneligibleReason.ForeignSchedule, result.Reason);
    }

    [Fact]
    public void Map_MigrationCohortSchedule_ProceedsToMapping()
    {
        var schedule = Schedule(new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
        });

        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(schedule: schedule), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.True(result.IsEligible);
    }

    [Fact]
    public void Map_NoScheduleAttached_ProceedsToMapping()
    {
        var result = AnnualUpgradeEligibilityMapper.Map(
            SubscriptionWith(), MonthlyTeamsPlan(), AnnualTeamsPlan());

        Assert.True(result.IsEligible);
    }

    [Fact]
    public void Map_ForeignScheduleAndUnusableDiscount_ReportsTheDiscount()
    {
        var subscription = UnexpandedDiscountSubscription();
        var schedule = Schedule(new Dictionary<string, string> { ["negotiated_term"] = "3y" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        Assert.Equal(
            AnnualUpgradeIneligibleReason.UnexpandedDiscounts,
            AnnualUpgradeEligibilityMapper.Map(subscription, MonthlyTeamsPlan(), AnnualTeamsPlan()).Reason);
    }
}
