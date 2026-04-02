using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Subscriptions.Commands;

public class ReinstateSubscriptionCommandTests
{
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<ReinstateSubscriptionCommand> _logger = Substitute.For<ILogger<ReinstateSubscriptionCommand>>();
    private readonly ReinstateSubscriptionCommand _command;

    public ReinstateSubscriptionCommandTests()
    {
        _command = new ReinstateSubscriptionCommand(_logger, _stripeAdapter, _featureService, _priceIncreaseScheduler);
    }

    [Fact]
    public async Task Run_SubscriptionNotPendingCancellation_ReturnsBadRequest()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription { Status = SubscriptionStatus.Active, CancelAt = null });

        var result = await _command.Run(user);

        Assert.True(result.IsT1);
        Assert.Equal("Subscription is not pending cancellation.", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_FlagOff_FallsThroughToStandardReinstate_NoScheduleCheck()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30)
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(false);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
    }

    [Fact]
    public async Task Run_FlagOn_NoSchedule_FallsThroughToStandardReinstate()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30),
                CustomerId = "cus_1",
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
                Items = new StripeList<SubscriptionItem> { Data = [] }
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
    }

    [Fact]
    public async Task Run_FlagOn_ScheduleExistsWithZeroPhases_FallsThroughToStandardReinstate()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30),
                CustomerId = "cus_1",
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
                Items = new StripeList<SubscriptionItem> { Data = [] }
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = []
                    }
                ]
            });

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_1",
            Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
    }

    [Fact]
    public async Task Run_FlagOn_OnePhaseCancelSchedule_NoMigratingPrice_ReturnsConflict()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };

        _stripeAdapter.GetSubscriptionAsync("sub_1")
            .Returns(new Subscription
            {
                Id = "sub_1",
                Status = SubscriptionStatus.Active,
                CancelAt = DateTime.UtcNow.AddDays(30),
                CustomerId = "cus_1",
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
                Items = new StripeList<SubscriptionItem>
                {
                    Data = [new SubscriptionItem { Price = new Price { Id = "non-migrating-price" }, Quantity = 1 }]
                }
            });

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = [new SubscriptionSchedulePhase()]
                    }
                ]
            });

        // ResolvePhase2Async returns null (no price migration path found)
        _priceIncreaseScheduler.ResolvePhase2Async(Arg.Any<Subscription>()).Returns((SubscriptionSchedulePhaseOptions)null);

        var result = await _command.Run(user);

        Assert.True(result.IsT2);
        var conflict = result.AsT2;
        Assert.Equal("We had a problem reinstating your subscription. Please contact support for assistance.", conflict.Response);
    }

    [Fact]
    public async Task Run_FlagOn_OnePhaseCancelSchedule_PremiumMigratingPrice_ReAddsPhase2WithReleaseEndBehavior()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };
        var scheduleStartDate = DateTime.UtcNow;
        var scheduleEndDate = scheduleStartDate.AddYears(1);
        var currentPeriodEnd = scheduleEndDate;

        var subscription = new Subscription
        {
            Id = "sub_1",
            Status = SubscriptionStatus.Active,
            CancelAt = DateTime.UtcNow.AddDays(30),
            CustomerId = "cus_1",
            Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "premium-old-seat" },
                        Quantity = 1,
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                ]
            }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_1").Returns(subscription);

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = scheduleStartDate,
                                EndDate = scheduleEndDate,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "premium-old-seat", Quantity = 1 }]
                            }
                        ]
                    }
                ]
            });

        var phase2 = new SubscriptionSchedulePhaseOptions
        {
            StartDate = currentPeriodEnd,
            Items =
            [
                new SubscriptionSchedulePhaseItemOptions
                {
                    Price = "premium-new-seat",
                    Quantity = 1
                }
            ],
            Discounts = [new SubscriptionSchedulePhaseDiscountOptions { Coupon = CouponIDs.Milestone2SubscriptionDiscount }],
            ProrationBehavior = ProrationBehavior.None
        };

        _priceIncreaseScheduler.ResolvePhase2Async(subscription).Returns(phase2);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync("sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                o.Phases.Count == 2 &&
                o.Phases[0].Items.Any(i => i.Price == "premium-old-seat") &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1) &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone2SubscriptionDiscount)));
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Run_FlagOn_OnePhaseCancelSchedule_Families2019MigratingPrice_ReAddsPhase2WithDiscount()
    {
        var organization = new Bit.Core.AdminConsole.Entities.Organization { GatewaySubscriptionId = "sub_1" };
        var orgId = organization.Id;
        var scheduleStartDate = DateTime.UtcNow;
        var scheduleEndDate = scheduleStartDate.AddYears(1);
        var currentPeriodEnd = scheduleEndDate;

        var families2019 = MockPlans.Get(PlanType.FamiliesAnnually2019);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        var subscription = new Subscription
        {
            Id = "sub_1",
            Status = SubscriptionStatus.Active,
            CancelAt = DateTime.UtcNow.AddDays(30),
            CustomerId = "cus_1",
            Metadata = new Dictionary<string, string> { ["organizationId"] = orgId.ToString() },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = families2019.PasswordManager.StripePlanId },
                        Quantity = 1,
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                ]
            }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_1").Returns(subscription);

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = scheduleStartDate,
                                EndDate = scheduleEndDate,
                                Items =
                                [
                                    new SubscriptionSchedulePhaseItem
                                    {
                                        PriceId = families2019.PasswordManager.StripePlanId,
                                        Quantity = 1
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var phase2 = new SubscriptionSchedulePhaseOptions
        {
            StartDate = currentPeriodEnd,
            Items =
            [
                new SubscriptionSchedulePhaseItemOptions
                {
                    Price = familiesTarget.PasswordManager.StripePlanId,
                    Quantity = 1
                }
            ],
            Discounts = [new SubscriptionSchedulePhaseDiscountOptions { Coupon = CouponIDs.Milestone3SubscriptionDiscount }],
            ProrationBehavior = ProrationBehavior.None
        };

        _priceIncreaseScheduler.ResolvePhase2Async(subscription).Returns(phase2);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync("sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Any(d => d.Coupon == CouponIDs.Milestone3SubscriptionDiscount)));
    }

    [Fact]
    public async Task Run_FlagOn_OnePhaseCancelSchedule_Families2025MigratingPrice_ReAddsPhase2WithoutDiscount()
    {
        var organization = new Bit.Core.AdminConsole.Entities.Organization { GatewaySubscriptionId = "sub_1" };
        var orgId = organization.Id;
        var scheduleStartDate = DateTime.UtcNow;
        var scheduleEndDate = scheduleStartDate.AddYears(1);
        var currentPeriodEnd = scheduleEndDate;

        var families2025 = MockPlans.Get(PlanType.FamiliesAnnually2025);
        var familiesTarget = MockPlans.Get(PlanType.FamiliesAnnually);

        var subscription = new Subscription
        {
            Id = "sub_1",
            Status = SubscriptionStatus.Active,
            CancelAt = DateTime.UtcNow.AddDays(30),
            CustomerId = "cus_1",
            Metadata = new Dictionary<string, string> { ["organizationId"] = orgId.ToString() },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = families2025.PasswordManager.StripePlanId },
                        Quantity = 1,
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                ]
            }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_1").Returns(subscription);

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = scheduleStartDate,
                                EndDate = scheduleEndDate,
                                Items =
                                [
                                    new SubscriptionSchedulePhaseItem
                                    {
                                        PriceId = families2025.PasswordManager.StripePlanId,
                                        Quantity = 1
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var phase2 = new SubscriptionSchedulePhaseOptions
        {
            StartDate = currentPeriodEnd,
            Items =
            [
                new SubscriptionSchedulePhaseItemOptions
                {
                    Price = familiesTarget.PasswordManager.StripePlanId,
                    Quantity = 1
                }
            ],
            Discounts = null,
            ProrationBehavior = ProrationBehavior.None
        };

        _priceIncreaseScheduler.ResolvePhase2Async(subscription).Returns(phase2);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync("sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                o.Phases.Count == 2 &&
                o.Phases[1].Items.Any(i => i.Price == familiesTarget.PasswordManager.StripePlanId && i.Quantity == 1) &&
                o.Phases[1].Discounts == null));
    }

    [Fact]
    public async Task Run_FlagOn_MultipleSchedules_SelectsActiveScheduleMatchingSubscription()
    {
        var user = new User { GatewaySubscriptionId = "sub_1" };
        var scheduleStartDate = DateTime.UtcNow;
        var scheduleEndDate = scheduleStartDate.AddYears(1);
        var currentPeriodEnd = scheduleEndDate;

        var subscription = new Subscription
        {
            Id = "sub_1",
            Status = SubscriptionStatus.Active,
            CancelAt = DateTime.UtcNow.AddDays(30),
            CustomerId = "cus_1",
            Metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString() },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "premium-old-seat" },
                        Quantity = 1,
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                ]
            }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_1").Returns(subscription);

        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

        // Return multiple schedules, but only one matches the subscription ID and is active
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_other_1",
                        SubscriptionId = "sub_other",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = [new SubscriptionSchedulePhase()]
                    },
                    new SubscriptionSchedule
                    {
                        Id = "sched_1",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = scheduleStartDate,
                                EndDate = scheduleEndDate,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "premium-old-seat", Quantity = 1 }]
                            }
                        ]
                    },
                    new SubscriptionSchedule
                    {
                        Id = "sched_completed",
                        SubscriptionId = "sub_1",
                        Status = SubscriptionScheduleStatus.Completed,
                        Phases = [new SubscriptionSchedulePhase()]
                    }
                ]
            });

        var phase2 = new SubscriptionSchedulePhaseOptions
        {
            StartDate = currentPeriodEnd,
            Items =
            [
                new SubscriptionSchedulePhaseItemOptions
                {
                    Price = "premium-new-seat",
                    Quantity = 1
                }
            ],
            Discounts = [new SubscriptionSchedulePhaseDiscountOptions { Coupon = CouponIDs.Milestone2SubscriptionDiscount }],
            ProrationBehavior = ProrationBehavior.None
        };

        _priceIncreaseScheduler.ResolvePhase2Async(subscription).Returns(phase2);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);
        // Should only update the matching active schedule for sub_1
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync("sched_1",
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                o.Phases.Count == 2 &&
                o.Phases[0].Items.Any(i => i.Price == "premium-old-seat") &&
                o.Phases[1].Items.Any(i => i.Price == "premium-new-seat" && i.Quantity == 1)));
        // Should not update other schedules
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync("sched_other_1", Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync("sched_completed", Arg.Any<SubscriptionScheduleUpdateOptions>());
    }
}
