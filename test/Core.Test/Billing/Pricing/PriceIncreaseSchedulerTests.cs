using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
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
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly ILogger<PriceIncreaseScheduler> _logger = Substitute.For<ILogger<PriceIncreaseScheduler>>();

    private PriceIncreaseScheduler CreateSut() =>
        new(_stripeAdapter, _featureService, _pricingClient, _organizationRepository, _assignmentRepository, _cohortRepository, _logger);

    [Fact]
    public async Task SchedulePersonalPriceIncrease_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(CreateSubscription("sub_1", "cus_1"));

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_ActiveScheduleAlreadyExists_Skips()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var subscription = CreateSubscription("sub_1", "cus_1");

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data = [CreateSchedule("sched_1", "sub_1", SubscriptionScheduleStatus.Active)]
            });

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_PremiumSubscription_CreatesScheduleWithMilestone2Discount()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1) &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone2SubscriptionDiscount) &&
                o.Phases[1].EndDate != null &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_PremiumSubscriptionWithExistingDiscount_PreservesDiscountAndAppendsMilestone2()
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
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "existing-grandfather-discount" } } }
        ];

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Discounts.Count == 2 &&
                o.Phases[1].Discounts[0].Coupon == "existing-grandfather-discount" &&
                o.Phases[1].Discounts[1].Coupon == CouponIDs.Milestone2SubscriptionDiscount));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_PremiumSubscriptionWithMultipleExistingDiscounts_PreservesAllAndAppendsMilestone2()
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
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "existing-grandfather-discount" } } },
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "existing-nfr-discount" } } }
        ];

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Discounts.Count == 3 &&
                o.Phases[1].Discounts[0].Coupon == "existing-grandfather-discount" &&
                o.Phases[1].Discounts[1].Coupon == "existing-nfr-discount" &&
                o.Phases[1].Discounts[2].Coupon == CouponIDs.Milestone2SubscriptionDiscount));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_PremiumSubscriptionWithStorage_IncludesStorageInPhase2()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1) &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-storage" && i.Quantity == 2)));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_Families2019Subscription_CreatesScheduleWithMilestone3Discount()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone3SubscriptionDiscount) &&
                o.Phases[1].EndDate != null &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_Families2019SubscriptionWithExistingDiscount_PreservesDiscountAndAppendsMilestone3()
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
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "existing-partner-discount" } } }
        ];

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Discounts.Count == 2 &&
                o.Phases[1].Discounts[0].Coupon == "existing-partner-discount" &&
                o.Phases[1].Discounts[1].Coupon == CouponIDs.Milestone3SubscriptionDiscount));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_Families2025Subscription_CreatesScheduleWithNoDiscount()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts == null &&
                o.Phases[1].EndDate != null &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_Families2025SubscriptionWithExistingDiscount_PreservesDiscountWithoutMilestone()
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
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "existing-retention-discount" } } }
        ];

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts[0].Coupon == "existing-retention-discount"));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_FamiliesSubscriptionWithStorage_IncludesStorageInPhase2()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripeStoragePlanId && i.Quantity == 3)));
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_UpdateFails_ReleasesOrphanedScheduleAndRethrows()
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

        await Assert.ThrowsAsync<StripeException>(() => sut.SchedulePersonalPriceIncrease(subscription));

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync("sched_1", null);
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_NoMatchingPlan_LogsWarningAndDoesNothing()
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

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_SubscriptionLoadedWithoutDiscountsExpand_DoesNotCreateSchedule()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        // Construct the subscription via the same JSON path Stripe.NET uses on API responses.
        // Verified empirically against Stripe.net 48.5.0: when "discounts" is not in the request's Expand list,
        // the SDK populates DiscountIds with the IDs and Discounts with a same-length list of null entries.
        // Direct assignment of `[null]` to subscription.Discounts is rejected by the SDK setter, so this is the
        // only way to reproduce the state in a unit test.
        const string unexpandedJson = """
            {
              "id": "sub_1",
              "object": "subscription",
              "customer": "cus_1",
              "metadata": { "userId": "00000000-0000-0000-0000-000000000001" },
              "discounts": ["di_abc"]
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _pricingClient.DidNotReceiveWithAnyArgs().ListPremiumPlans();
        await _pricingClient.DidNotReceiveWithAnyArgs().GetPlanOrThrow(Arg.Any<PlanType>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_ProviderSubscription_DoesNotCreateSchedule()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var providerMetadata = new Dictionary<string, string> { { "providerId", Guid.NewGuid().ToString() } };
        var subscription = CreateSubscription("sub_1", "cus_1", providerMetadata,
            CreateSubscriptionItem("some-price-id", 1));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task Release_BothFeatureFlagsOff_StillReleasesWhenScheduleExists()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

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
    public async Task Release_PM35215EnabledOnly_StillReleases()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

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

    // --- Business path tests ---

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

        var sut = CreateSut();

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_ActiveScheduleAlreadyExists_Skips()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data = [CreateSchedule("sched_1", "sub_1", SubscriptionScheduleStatus.Active)]
            });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_EnterpriseAnnually2020ToCurrent_CreatesScheduleAndStampsAssignment()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow;
        var periodLength = TimeSpan.FromDays(365);
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10, periodStart, periodLength),
            CreateSubscriptionItem(source.PasswordManager.StripeStoragePlanId, 2, periodStart, periodLength));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        };
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.True(result);

        var expectedPhase2Start = periodStart + periodLength;
        var expectedPhase2End = expectedPhase2Start + periodLength;
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeSeatPlanId && i.Quantity == 10) &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeStoragePlanId && i.Quantity == 2) &&
                o.Phases[1].StartDate == expectedPhase2Start &&
                o.Phases[1].EndDate == expectedPhase2End &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));

        await _assignmentRepository.Received(1).ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
            a.OrganizationId == orgId && a.ScheduledDate != null));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_OnSuccess_StampsCohortMetadataOnSchedulePhases()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        };
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.True(result);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata != null &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == cohort.Id.ToString() &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortName] == cohort.Name &&
                o.Phases[1].Metadata != null &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == cohort.Id.ToString() &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortName] == cohort.Name));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            "sub_1", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_DoesNotInvokeUpdateSubscription()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task SchedulePersonalPriceIncrease_DoesNotSetMetadataOnPhases()
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
        subscription.Items.Data[0].CurrentPeriodEnd = currentPeriodEnd;

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();

        await sut.SchedulePersonalPriceIncrease(subscription);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata == null &&
                o.Phases[1].Metadata == null));
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_LineItemUsingMapper_PicksUpSecretsManagerSeat()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.SecretsManager.StripeSeatPlanId, 4));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Any(i => i.Price == target.SecretsManager.StripeSeatPlanId && i.Quantity == 4)));
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_EnterpriseMonthly2020ToCurrent_CreatesScheduleAndStampsAssignment()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseMonthly2020);
        var target = MockPlans.Get(PlanType.EnterpriseMonthly);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(target);

        var orgId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow;
        var periodLength = TimeSpan.FromDays(30);
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 5, periodStart, periodLength),
            CreateSubscriptionItem(source.PasswordManager.StripeStoragePlanId, 1, periodStart, periodLength));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020MonthlyToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        };
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.True(result);

        var expectedPhase2Start = periodStart + periodLength;
        var expectedPhase2End = expectedPhase2Start + periodLength;
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeSeatPlanId && i.Quantity == 5) &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeStoragePlanId && i.Quantity == 1) &&
                o.Phases[1].StartDate == expectedPhase2Start &&
                o.Phases[1].EndDate == expectedPhase2End &&
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release));

        await _assignmentRepository.Received(1).ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
            a.OrganizationId == orgId && a.ScheduledDate != null));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithSubscriptionDiscounts_PreservesDiscounts()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "grandfather" } } }
        ];
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts[0].Coupon == "grandfather"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithCustomerAndSubscriptionDiscounts_PreservesBoth()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "grandfather" } } }
        ];
        subscription.Customer = new Customer
        {
            Id = "cus_1",
            Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } }
        };
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 2 &&
                o.Phases[1].Discounts[0].Coupon == "retention" &&
                o.Phases[1].Discounts[1].Coupon == "grandfather"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithCustomerDiscountOnly_IncludesCustomerDiscount()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        subscription.Customer = new Customer
        {
            Id = "cus_1",
            Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } }
        };
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts[0].Coupon == "retention"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithCohortProactiveCoupon_AppendsAsLast()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "grandfather" } } }
        ];
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent, proactiveCoupon: "PROACT-25");

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 2 &&
                o.Phases[1].Discounts[0].Coupon == "grandfather" &&
                o.Phases[1].Discounts[1].Coupon == "PROACT-25"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithoutCohortProactiveCoupon_OmitsIt()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts =
        [
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "grandfather" } } }
        ];
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent, proactiveCoupon: null);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Count == 1 &&
                o.Phases[1].Discounts[0].Coupon == "grandfather"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithSecretsManagerLineItems_MapsSeatAndServiceAccount()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10),
            CreateSubscriptionItem(source.SecretsManager.StripeSeatPlanId, 4),
            CreateSubscriptionItem(source.SecretsManager.StripeServiceAccountPlanId, 50));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Count == 3 &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeSeatPlanId && i.Quantity == 10) &&
                o.Phases[1].Items.Any(i => i.Price == target.SecretsManager.StripeSeatPlanId && i.Quantity == 4) &&
                o.Phases[1].Items.Any(i => i.Price == target.SecretsManager.StripeServiceAccountPlanId && i.Quantity == 50)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_WithStorage_PreservesStorageQuantity()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10),
            CreateSubscriptionItem(source.PasswordManager.StripeStoragePlanId, 3));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        });

        var sut = CreateSut();

        await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeStoragePlanId && i.Quantity == 3)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_StripeUpdateFails_ReleasesOrphanAndDoesNotStampAssignment()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _stripeAdapter.UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>())
            .ThrowsAsync(new StripeException("update failed"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<StripeException>(() => sut.ScheduleBusinessPriceIncrease(subscription, cohort));

        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync("sched_1", null);
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_UnknownLineItemPrice_LogsWarningAndDoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem("unknown-price-id", 1));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_ChurnOnlyCohort_ReturnsFalseSilently()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem("2020-enterprise-org-seat-annually", 10));
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = Guid.NewGuid(),
            Name = "churn-only-cohort",
            MigrationPathId = null
        };

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("MigrationPathId")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_AssignmentRowMissing_LogsErrorButReturnsTrue()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.True(result);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1", Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_SubscriptionDiscountsContainNullEntries_LogsErrorAndReturnsFalse()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        // Construct the subscription via the same JSON path Stripe.NET uses on API responses.
        // When "discounts" is not in the request's Expand list, the SDK populates DiscountIds with
        // the IDs and Discounts with a same-length list of null entries. The business path mirrors
        // the personal path's upfront guard (Q10 in the plan) — the whole call must reject.
        var orgId = Guid.NewGuid();
        var unexpandedJson = $$"""
            {
              "id": "sub_1",
              "object": "subscription",
              "customer": "cus_1",
              "metadata": { "organizationId": "{{orgId}}" },
              "discounts": ["di_abc"]
            }
            """;
        var subscription = Newtonsoft.Json.JsonConvert.DeserializeObject<Subscription>(unexpandedJson)!;
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _pricingClient.DidNotReceiveWithAnyArgs().GetPlanOrThrow(Arg.Any<PlanType>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_UserSubscription_LogsWarningAndDoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var subscription = CreateSubscription("sub_1", "cus_1",
            new Dictionary<string, string> { { "userId", Guid.NewGuid().ToString() } });
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_ProviderSubscription_LogsWarningAndDoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var subscription = CreateSubscription("sub_1", "cus_1",
            new Dictionary<string, string> { { "providerId", Guid.NewGuid().ToString() } });
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ScheduleBusinessPriceIncrease_MissingSubscriberMetadata_LogsErrorAndDoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var subscription = CreateSubscription("sub_1", "cus_1",
            new Dictionary<string, string>());
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();

        var result = await sut.ScheduleBusinessPriceIncrease(subscription, cohort);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ScheduleForSubscription_UserSubscription_RoutesToPersonalPath_CreatesSchedule()
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

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.True(result);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat")));
    }

    [Fact]
    public async Task ScheduleForSubscription_TrackAOrg_ActiveCohortMatchingPlan_RoutesToBusinessPath_CreatesSchedule()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id
        };

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually2020));
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohort.Id).Returns(cohort);

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.True(result);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == target.PasswordManager.StripeSeatPlanId)));
    }

    [Fact]
    public async Task ScheduleForSubscription_TrackAOrg_NoAssignment_ReturnsFalse()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually2020));
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns((OrganizationPlanMigrationCohortAssignment?)null);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleForSubscription_TrackAOrg_InactiveCohort_ReturnsFalse()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var inactiveCohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "inactive",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = false
        };

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually2020));
        _assignmentRepository.GetByOrganizationIdAsync(orgId)
            .Returns(new OrganizationPlanMigrationCohortAssignment { OrganizationId = orgId, CohortId = cohortId });
        _cohortRepository.GetByIdAsync(cohortId).Returns(inactiveCohort);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleForSubscription_TrackAOrg_PlanTypeDrifted_ReturnsFalse()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);

        // Org PlanType is EnterpriseAnnually (already migrated), not EnterpriseAnnually2020
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually));
        _assignmentRepository.GetByOrganizationIdAsync(orgId)
            .Returns(new OrganizationPlanMigrationCohortAssignment { OrganizationId = orgId, CohortId = cohort.Id });
        _cohortRepository.GetByIdAsync(cohort.Id).Returns(cohort);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleForSubscription_NonTrackAOrg_FamiliesOrg_RoutesToPersonalPath()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        var orgId = Guid.NewGuid();
        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);
        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesTarget);

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.FamiliesAnnually2019));

        var subscription = CreateSubscription("sub_1", "cus_1",
            new Dictionary<string, string> { { "organizationId", orgId.ToString() } },
            CreateSubscriptionItem(families2019.PasswordManager.StripePlanId, 1));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.True(result);
        await _stripeAdapter.Received(1).CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
        await _cohortRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await _assignmentRepository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ScheduleForSubscription_ProviderSubscription_ReturnsFalse()
    {
        var providerId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_1", "cus_1",
            new Dictionary<string, string> { { "providerId", providerId.ToString() } });

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleForSubscription_SkipIfAlreadyScheduled_ScheduledDateSet_ReturnsFalse()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var orgId = Guid.NewGuid();
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id,
            ScheduledDate = DateTime.UtcNow
        };

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually2020));
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(
            subscription,
            new OrganizationPriceIncreaseOptions { SkipIfAlreadyScheduled = true });

        Assert.False(result);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
    }

    [Fact]
    public async Task ScheduleForSubscription_DefaultOptions_ScheduledDateSet_ProceedsToSchedule()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(target);

        var orgId = Guid.NewGuid();
        var cohort = CreateCohort(MigrationPathId.Enterprise2020AnnualToCurrent);
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CohortId = cohort.Id,
            ScheduledDate = DateTime.UtcNow  // already scheduled, but no SkipIfAlreadyScheduled guard
        };

        _organizationRepository.GetByIdAsync(orgId)
            .Returns(CreateOrganization(orgId, PlanType.EnterpriseAnnually2020));
        _assignmentRepository.GetByOrganizationIdAsync(orgId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohort.Id).Returns(cohort);

        var subscription = CreateBusinessSubscription("sub_1", "cus_1", orgId,
            CreateSubscriptionItem(source.PasswordManager.StripeSeatPlanId, 10));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _stripeAdapter.CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(CreateScheduleWithPhase("sched_1", "sub_1"));

        var sut = CreateSut();
        var result = await sut.ScheduleForSubscription(subscription);  // default options

        Assert.True(result);
        await _stripeAdapter.Received(1)
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>());
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

    private static Subscription CreateBusinessSubscription(string id, string customerId, Guid organizationId, params SubscriptionItem[] items) =>
        CreateSubscription(
            id,
            customerId,
            new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            items);

    private static SubscriptionItem CreateSubscriptionItem(
        string priceId,
        long quantity,
        DateTime? periodStart = null,
        TimeSpan? periodLength = null)
    {
        var start = periodStart ?? DateTime.UtcNow;
        var length = periodLength ?? TimeSpan.FromDays(365);
        return new SubscriptionItem
        {
            Price = new Price { Id = priceId },
            Quantity = quantity,
            CurrentPeriodStart = start,
            CurrentPeriodEnd = start + length
        };
    }

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

    private static OrganizationPlanMigrationCohort CreateCohort(
        MigrationPathId pathId,
        string? proactiveCoupon = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"cohort-{pathId}",
            MigrationPathId = pathId,
            ProactiveDiscountCouponCode = proactiveCoupon,
            IsActive = true
        };

    private static Organization CreateOrganization(Guid id, PlanType planType) =>
        new() { Id = id, PlanType = planType };


}
