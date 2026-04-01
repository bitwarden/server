using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using Purchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Pricing;

public class PriceIncreaseSchedulerTests
{
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly ILogger<PriceIncreaseScheduler> _logger = Substitute.For<ILogger<PriceIncreaseScheduler>>();

    private PriceIncreaseScheduler CreateSut() =>
        new(_stripeAdapter, _featureService, _pricingClient, _logger);

    [Fact]
    public async Task Schedule_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);

        var sut = CreateSut();

        await sut.Schedule(CreateSubscription("sub_1", "cus_1"));

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Fact]
    public async Task Schedule_ActiveScheduleAlreadyExists_Skips()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var subscription = CreateSubscription("sub_1", "cus_1");

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data = [CreateSchedule("sched_1", "sub_1", SubscriptionScheduleStatus.Active)]
            });

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Schedule_PremiumSubscription_CreatesScheduleWithMilestone2Discount()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var oldPremium = new PremiumPlan
        {
            Name = "Premium (Old)",
            Available = false,
            Seat = new Purchasable { StripePriceId = "premium-old-seat", Price = 10, Provided = 1 },
            Storage = new Purchasable { StripePriceId = "premium-old-storage", Price = 4, Provided = 1 }
        };

        var newPremium = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            Seat = new Purchasable { StripePriceId = "premium-new-seat", Price = 15, Provided = 1 },
            Storage = new Purchasable { StripePriceId = "premium-new-storage", Price = 4, Provided = 1 }
        };

        _pricingClient.ListPremiumPlans().Returns([oldPremium, newPremium]);

        var subscription = CreateSubscription("sub_1", "cus_1",
            CreateSubscriptionItem("premium-old-seat", 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var createdSchedule = CreateScheduleWithPhase("sched_1", "sub_1");

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(createdSchedule);

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1) &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone2SubscriptionDiscount) &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task Schedule_PremiumSubscriptionWithStorage_IncludesStorageInPhase2()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var oldPremium = new PremiumPlan
        {
            Name = "Premium (Old)",
            Available = false,
            Seat = new Purchasable { StripePriceId = "premium-old-seat", Price = 10, Provided = 1 },
            Storage = new Purchasable { StripePriceId = "premium-old-storage", Price = 4, Provided = 1 }
        };

        var newPremium = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            Seat = new Purchasable { StripePriceId = "premium-new-seat", Price = 15, Provided = 1 },
            Storage = new Purchasable { StripePriceId = "premium-new-storage", Price = 4, Provided = 1 }
        };

        _pricingClient.ListPremiumPlans().Returns([oldPremium, newPremium]);

        var subscription = CreateSubscription("sub_1", "cus_1",
            CreateSubscriptionItem("premium-old-seat", 1),
            CreateSubscriptionItem("premium-old-storage", 2));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1) &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-storage" && i.Quantity == 2)));
    }

    [Fact]
    public async Task Schedule_Families2019Subscription_CreatesScheduleWithMilestone3Discount()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        // Return empty premium plans so it falls through to families logic
        _pricingClient.ListPremiumPlans().Returns([]);

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        var orgMetadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", orgMetadata,
            CreateSubscriptionItem(families2019.PasswordManager.StripePlanId, 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var createdSchedule = CreateScheduleWithPhase("sched_1", "sub_1");

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(createdSchedule);

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone3SubscriptionDiscount) &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task Schedule_Families2025Subscription_CreatesScheduleWithNoDiscount()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _pricingClient.ListPremiumPlans().Returns([]);

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        var orgMetadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", orgMetadata,
            CreateSubscriptionItem(families2025.PasswordManager.StripePlanId, 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var createdSchedule = CreateScheduleWithPhase("sched_1", "sub_1");

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(createdSchedule);

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts == null &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task Schedule_FamiliesSubscriptionWithStorage_IncludesStorageInPhase2()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _pricingClient.ListPremiumPlans().Returns([]);

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        var orgMetadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", orgMetadata,
            CreateSubscriptionItem(families2019.PasswordManager.StripePlanId, 1),
            CreateSubscriptionItem(families2019.PasswordManager.StripeStoragePlanId, 3));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripeStoragePlanId && i.Quantity == 3)));
    }

    [Fact]
    public async Task Schedule_UpdateFails_ReleasesOrphanedScheduleAndRethrows()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _pricingClient.ListPremiumPlans().Returns([]);

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        var orgMetadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", orgMetadata,
            CreateSubscriptionItem(families2019.PasswordManager.StripePlanId, 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var createdSchedule = CreateScheduleWithPhase("sched_1", "sub_1");

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(createdSchedule);

        _stripeAdapter.UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>())
            .ThrowsAsync(new StripeException("update failed"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<StripeException>(() => sut.Schedule(subscription));

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync("sched_1", null);
    }

    [Fact]
    public async Task Schedule_NoMatchingPlan_LogsWarningAndDoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _pricingClient.ListPremiumPlans().Returns([]);

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        // Subscription with a price that doesn't match any known plan
        var orgMetadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", orgMetadata,
            CreateSubscriptionItem("unknown-price-id", 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        await sut.Schedule(subscription);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }


    [Fact]
    public async Task Release_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);

        var sut = CreateSut();

        await sut.Release("cus_1", "sub_1");

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Fact]
    public async Task Release_ActiveScheduleExists_ReleasesIt()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data = [CreateSchedule("sched_1", "sub_1", SubscriptionScheduleStatus.Active)]
            });

        var sut = CreateSut();

        await sut.Release("cus_1", "sub_1");

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync("sched_1", null);
    }

    [Fact]
    public async Task Release_NoActiveSchedule_DoesNotRelease()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        await sut.Release("cus_1", "sub_1");

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ReleaseSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleReleaseOptions>());
    }

    [Fact]
    public async Task Release_ScheduleForDifferentSubscription_DoesNotRelease()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data = [CreateSchedule("sched_1", "sub_other", SubscriptionScheduleStatus.Active)]
            });

        var sut = CreateSut();

        await sut.Release("cus_1", "sub_1");

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ReleaseSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleReleaseOptions>());
    }

    [Fact]
    public async Task Release_ReleaseThrows_LogsErrorAndRethrows()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .ThrowsAsync(new StripeException("list failed"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<StripeException>(() => sut.Release("cus_1", "sub_1"));
    }

    private static Subscription CreateSubscription(string id, string customerId, params SubscriptionItem[] items) =>
        CreateSubscription(id, customerId, new Dictionary<string, string> { { "userId", Guid.NewGuid().ToString() } }, items);

    private static Subscription CreateSubscription(string id, string customerId, Dictionary<string, string> metadata, params SubscriptionItem[] items) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            Metadata = metadata,
            Items = new StripeList<SubscriptionItem> { Data = [.. items] }
        };

    private static SubscriptionItem CreateSubscriptionItem(string priceId, long quantity) =>
        new()
        {
            Price = new Price { Id = priceId },
            Quantity = quantity
        };

    private static SubscriptionSchedule CreateSchedule(string id, string subscriptionId, string status) =>
        new()
        {
            Id = id,
            SubscriptionId = subscriptionId,
            Status = status
        };

    private static SubscriptionSchedule CreateScheduleWithPhase(string id, string subscriptionId)
    {
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddYears(1);

        return new SubscriptionSchedule
        {
            Id = id,
            SubscriptionId = subscriptionId,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem { PriceId = "phase-1-price", Quantity = 1 }
                    ]
                }
            ]
        };
    }
}
