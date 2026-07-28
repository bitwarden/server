using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Services;

using static StripeConstants;

[SutProviderCustomize]
public class StripePaymentServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithCustomerDiscount_ReturnsDiscountFromCustomer(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var customerDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 20m,
                AmountOff = 1400
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = customerDiscount
            },
            Discounts = new List<Discount>(), // Empty list
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
        Assert.Equal(14.00m, result.CustomerDiscount.AmountOff); // Converted from cents
        Assert.False(result.CustomerDiscount.IsFromSchedule); // Genuine customer discount, not schedule-derived
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithoutCustomerDiscount_FallsBackToSubscriptionDiscounts(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscriptionDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 15m,
                AmountOff = null
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            Discounts = new List<Discount> { subscriptionDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should use subscription discount as fallback
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(15m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithBothDiscounts_PrefersCustomerDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var customerDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 25m
            },
            End = null
        };

        var subscriptionDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "different-coupon-id",
                PercentOff = 10m
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = customerDiscount // Should prefer this
            },
            Discounts = new List<Discount> { subscriptionDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should prefer customer discount over subscription discount
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNoDiscounts_ReturnsNullDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null
            },
            Discounts = new List<Discount>(), // Empty list, no discounts
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithMultipleSubscriptionDiscounts_SelectsFirstDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Multiple subscription-level discounts, no customer discount
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var firstDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "coupon-10-percent",
                PercentOff = 10m
            },
            End = null
        };

        var secondDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "coupon-20-percent",
                PercentOff = 20m
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            // Multiple subscription discounts - FirstOrDefault() should select the first one
            Discounts = new List<Discount> { firstDiscount, secondDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should select the first discount from the list (FirstOrDefault() behavior)
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal("coupon-10-percent", result.CustomerDiscount.Id);
        Assert.Equal(10m, result.CustomerDiscount.PercentOff);
        // Verify the second discount was not selected
        Assert.NotEqual("coupon-20-percent", result.CustomerDiscount.Id);
        Assert.NotEqual(20m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNullCustomer_HandlesGracefully(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Subscription with null Customer (defensive null check scenario)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = null, // Customer not expanded or null
            Discounts = new List<Discount>(), // Empty discounts
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should handle null Customer gracefully without throwing NullReferenceException
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNullDiscounts_HandlesGracefully(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Subscription with null Discounts (defensive null check scenario)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            Discounts = null, // Discounts not expanded or null
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should handle null Discounts gracefully without throwing NullReferenceException
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_VerifiesCorrectExpandOptions(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(), // Empty list
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .GetSubscriptionAsync(
                Arg.Any<string>(),
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Verify expand options are correct
        await stripeAdapter.Received(1).GetSubscriptionAsync(
            subscriber.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o =>
                o.Expand.Contains("customer.discount.coupon.applies_to") &&
                o.Expand.Contains("discounts.coupon.applies_to") &&
                o.Expand.Contains("test_clock")));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithEmptyGatewaySubscriptionId_ReturnsEmptySubscriptionInfo(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.GatewaySubscriptionId = null;

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Subscription);
        Assert.Null(result.CustomerDiscount);
        Assert.Null(result.UpcomingInvoice);

        // Verify no Stripe API calls were made
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_OverridesPricesAndDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Nickname = "Families 2019", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        }
                    ],
                    Discounts =
                    [
                        new SubscriptionSchedulePhaseDiscount
                        {
                            Coupon = new Coupon { Id = CouponIDs.Milestone3SubscriptionDiscount, PercentOff = 25m }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — prices overridden with Phase 2 values
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(47.88m, item.Amount);

        // Assert — discount overridden with Phase 2 discount
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(CouponIDs.Milestone3SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
        Assert.True(result.CustomerDiscount.Active);
        Assert.True(result.CustomerDiscount.IsFromSchedule);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_NoPhase2Discount_KeepsOriginalDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer
            {
                Discount = new Discount
                {
                    Coupon = new Coupon { Id = "existing-coupon", PercentOff = 10m },
                    End = null
                }
            },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Nickname = "Families 2025", Amount = 4000, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        }
                    ],
                    Discounts = new List<SubscriptionSchedulePhaseDiscount>()
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — price overridden
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(47.88m, item.Amount);

        // Assert — original discount preserved (Phase 2 has no discount)
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal("existing-coupon", result.CustomerDiscount.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNoSchedule_DoesNotFetchSchedule(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = null,
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — original price preserved
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(12.00m, item.Amount);

        // Assert — no schedule fetch
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .GetSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleGetOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_ScheduleFetchFails_GracefullyFallsBack(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .ThrowsAsync(new StripeException("Schedule not found"));

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — original data preserved despite schedule fetch failure
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(12.00m, item.Amount);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_ScheduleNotActive_DoesNotOverride(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Canceled,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families" }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — original price preserved
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(12.00m, item.Amount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_Phase2AlreadyStarted_DoesNotOverride(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_families", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-60) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(-5),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families" }
                        }
                    ],
                    Discounts =
                    [
                        new SubscriptionSchedulePhaseDiscount
                        {
                            Coupon = new Coupon { Id = CouponIDs.Milestone3SubscriptionDiscount, PercentOff = 25m }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — original price preserved, Phase 2 already started so no override
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(12.00m, item.Amount);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_CrossProductMigration_OverridesPriceProductIdAndName(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — Phase 1 item uses a different Stripe product than Phase 2 (Families 2019 → current)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_old_families", Nickname = "Families 2019", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        }
                    ],
                    Discounts =
                    [
                        new SubscriptionSchedulePhaseDiscount
                        {
                            Coupon = new Coupon { Id = CouponIDs.Milestone3SubscriptionDiscount, PercentOff = 25m }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — price, product ID, and name overridden with Phase 2 values
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal(47.88m, item.Amount);
        Assert.Equal("prod_families", item.ProductId);
        Assert.Equal("Families", item.Name);

        // Assert — discount overridden with Phase 2 discount
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(CouponIDs.Milestone3SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_CrossProductMigration_WithStorage_OverridesCorrectly(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — storage matches by product ID (Pass 1), main plan falls back (Pass 2)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_old_families", Nickname = "Families 2019", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    },
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_storage", Nickname = "Storage", Amount = 400, Interval = "year" },
                        Quantity = 2
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        },
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 400, ProductId = "prod_storage", Nickname = "Storage" }
                        }
                    ],
                    Discounts =
                    [
                        new SubscriptionSchedulePhaseDiscount
                        {
                            Coupon = new Coupon { Id = CouponIDs.Milestone3SubscriptionDiscount, PercentOff = 25m }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — main plan overridden via fallback
        var items = result.Subscription!.Items.ToList();
        Assert.Equal(2, items.Count);

        var mainItem = items.First(i => i.ProductId == "prod_families");
        Assert.Equal(47.88m, mainItem.Amount);
        Assert.Equal("Families", mainItem.Name);

        // Assert — storage matched by product ID, amount updated
        var storageItem = items.First(i => i.ProductId == "prod_storage");
        Assert.Equal(4.00m, storageItem.Amount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_CrossProductMigration_SkipsAddonItems(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — Phase 1 has main plan + addon; fallback should pick main plan, not addon
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Metadata = new Dictionary<string, string> { { "isAddOn", "true" } } },
                        Plan = new Plan { ProductId = "prod_premium_access", Nickname = "Premium Access", Amount = 0, Interval = "year" },
                        Quantity = 1
                    },
                    new SubscriptionItem
                    {
                        Plan = new Plan { ProductId = "prod_old_families", Nickname = "Families 2019", Amount = 1200, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        }
                    ],
                    Discounts = new List<SubscriptionSchedulePhaseDiscount>()
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — main plan overridden, addon untouched
        var items = result.Subscription!.Items.ToList();
        Assert.Equal(2, items.Count);

        var mainItem = items.First(i => i.ProductId == "prod_families");
        Assert.Equal(47.88m, mainItem.Amount);
        Assert.Equal("Families", mainItem.Name);

        var addonItem = items.First(i => i.ProductId == "prod_premium_access");
        Assert.Equal(0m, addonItem.Amount);
        Assert.True(addonItem.AddonSubscriptionItem);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithActiveSchedule_CrossProductMigration_NoFallbackTarget_GracefullyIgnored(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — Phase 1 has only an addon item; no eligible fallback target for Phase 2
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Metadata = new Dictionary<string, string> { { "isAddOn", "true" } } },
                        Plan = new Plan { ProductId = "prod_premium_access", Nickname = "Premium Access", Amount = 0, Interval = "year" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { UnitAmount = 4788, ProductId = "prod_families", Nickname = "Families" }
                        }
                    ],
                    Discounts = new List<SubscriptionSchedulePhaseDiscount>()
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — addon item is untouched, Phase 2 item was not applied
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal("prod_premium_access", item.ProductId);
        Assert.Equal(0m, item.Amount);
        Assert.True(item.AddonSubscriptionItem);
    }

    [Theory, BitAutoData]
    public async Task AdjustSubscription_WhenCustomerIsExempt_DoesNotUpdateTaxExemption(
        SutProvider<StripePaymentService> sutProvider,
        Organization organization)
    {
        var plan = new EnterprisePlan(isAnnual: true);
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.GatewaySubscriptionId = "sub_123";
        organization.Seats = 0;
        organization.UseSecretsManager = false;
        organization.MaxStorageGb = null;

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = "active",
            Customer = new Customer
            {
                Id = "cus_123",
                Address = new Address { Country = "DE" },
                TaxExempt = TaxExempt.Exempt
            },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId },
                        Plan = new Stripe.Plan { Id = plan.PasswordManager.StripeSeatPlanId },
                        Quantity = 0
                    }
                ]
            }
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.EnterpriseAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(new Subscription { Id = "sub_123", LatestInvoiceId = "inv_123" });

        sutProvider.GetDependency<IStripeAdapter>()
            .GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>())
            .Returns(new Invoice { Id = "inv_123", AmountDue = 0, Status = InvoiceStatus.Paid });

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCustomerAsync("cus_123")
            .Returns(new Customer { Id = "cus_123" });

        await sutProvider.Sut.AdjustSubscription(organization, plan, 0, false, null, null, 0);

        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_PackagedSourceWithSchedule_PreviewCarriesPhase2SeatQuantity(
        SutProvider<StripePaymentService> sutProvider,
        Organization subscriber)
    {
        // A packaged source has one flat base line (qty 1). Its pending migration schedule collapses
        // that onto a scalable seat line at the migrated quantity (3). The Phase 2 preview must adopt
        // both the seat price AND that quantity, otherwise the total is shown at the base line's qty 1.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test";
        subscriber.GatewaySubscriptionId = "sub_test";

        var subscription = new Subscription
        {
            Id = "sub_test",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Quantity = 1,
                        Plan = new Plan
                        {
                            Id = "teams-org-annually",
                            ProductId = "prod_teams2019",
                            Nickname = "2019 Teams Organization",
                            Amount = 6000, // $60 flat base bundle
                            Interval = "year"
                        }
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase(),
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddYears(1), // future -> not yet applied
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Quantity = 3, // migrated seat count (occupied)
                            Price = new Price
                            {
                                Id = "2023-teams-org-seat-annually",
                                ProductId = "prod_teams_current",
                                UnitAmount = 4800, // $48 per seat
                                Nickname = "Teams Organization Seat (Annually)"
                            }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync(subscription.ScheduleId, Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        var item = Assert.Single(result.Subscription.Items);
        Assert.Equal("Teams Organization Seat (Annually)", item.Name);
        Assert.Equal(48m, item.Amount);
        Assert.Equal(3, item.Quantity); // carried from Phase 2, not the base line's 1
        Assert.Equal(144m, item.Amount * item.Quantity);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_TeamsStarterWithSchedule_PreviewCarriesPhase2SeatQuantity(
        SutProvider<StripePaymentService> sutProvider,
        Organization subscriber)
    {
        // Teams Starter is a flat bundle (base line, qty 1) migrating to the current Teams per-seat
        // line. Same cross-product collapse as Teams 2019: the preview must show the migrated seat
        // count, not the base line's qty 1.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test";
        subscriber.GatewaySubscriptionId = "sub_test";

        var subscription = new Subscription
        {
            Id = "sub_test",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Quantity = 1,
                        Plan = new Plan
                        {
                            Id = "teams-org-starter",
                            ProductId = "prod_teams_starter",
                            Nickname = "Teams (Starter)",
                            Amount = 2000, // $20 flat bundle
                            Interval = "month"
                        }
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase(),
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddMonths(1), // future -> not yet applied
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Quantity = 4, // migrated seat count (occupied)
                            Price = new Price
                            {
                                Id = "2023-teams-org-seat-monthly",
                                ProductId = "prod_teams_current",
                                UnitAmount = 400, // $4 per seat / month
                                Nickname = "Teams Organization Seat (Monthly)"
                            }
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync(subscription.ScheduleId, Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        var item = Assert.Single(result.Subscription.Items);
        Assert.Equal("Teams Organization Seat (Monthly)", item.Name);
        Assert.Equal(4m, item.Amount);
        Assert.Equal(4, item.Quantity); // carried from Phase 2, not the base line's 1
        Assert.Equal(16m, item.Amount * item.Quantity);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithMigrationGraceMetadata_MapsServiceAccountGrace(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — a migrated subscription carries the free service-account grace in metadata.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string>
            {
                { MetadataKeys.MigrationGraceServiceAccounts, "30" }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — grace is read off metadata onto the wrapper using the already-fetched subscription.
        Assert.Equal(30, result.Subscription!.ServiceAccountGrace);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithoutMigrationGraceMetadata_ServiceAccountGraceIsZero(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange — Metadata intentionally left null (non-migrated subscription); the read must not throw.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.Equal(0, result.Subscription!.ServiceAccountGrace);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_Teams2019BaseAndOverage_CollapsesToSingleSeatLine(
        SutProvider<StripePaymentService> sutProvider,
        Organization subscriber)
    {
        // Arrange — a Teams 2019 org billed as a base bundle line + a seat-overage add-on line, with a
        // pending migration schedule whose Phase 2 collapses both into one current-Teams seat line (x7).
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";
        subscriber.PlanType = PlanType.TeamsMonthly2019;

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "teams-org-seat-monthly" },
                        Plan = new Plan { Id = "teams-org-seat-monthly", ProductId = "prod_2019_teams_seat", Nickname = "2019 Teams Seat (Monthly)", Amount = 250, Interval = "month" },
                        Quantity = 2
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "teams-org-monthly" },
                        Plan = new Plan { Id = "teams-org-monthly", ProductId = "prod_2019_teams_org", Nickname = "2019 Teams Org. (Monthly)", Amount = 800, Interval = "month" },
                        Quantity = 1
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { Id = "teams-current-seat", UnitAmount = 400, ProductId = "prod_current_teams", Nickname = "Teams Organization Seat" },
                            Quantity = 7
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsMonthly2019)
            .Returns(new Teams2019Plan(false));

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — a single migrated seat line at the Phase 2 price/quantity; no surviving legacy amount.
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal("prod_current_teams", item.ProductId);
        Assert.Equal("Teams Organization Seat", item.Name);
        Assert.Equal(4.00m, item.Amount);
        Assert.Equal(7, item.Quantity);
        Assert.DoesNotContain(result.Subscription.Items, i => i.PriceId == "teams-org-seat-monthly");
        Assert.DoesNotContain(result.Subscription.Items, i => i.Amount == 8.00m);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_Teams2019WithStorageAddon_DropsOverageButKeepsStorage(
        SutProvider<StripePaymentService> sutProvider,
        Organization subscriber)
    {
        // Arrange — same collapse, plus a storage add-on whose product changes across the migration
        // (cross-product). Only the seat-overage line collapses; the storage line must survive the preview.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";
        subscriber.PlanType = PlanType.TeamsMonthly2019;

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "teams-org-monthly" },
                        Plan = new Plan { Id = "teams-org-monthly", ProductId = "prod_2019_teams_org", Nickname = "2019 Teams Org. (Monthly)", Amount = 800, Interval = "month" },
                        Quantity = 1
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "teams-org-seat-monthly" },
                        Plan = new Plan { Id = "teams-org-seat-monthly", ProductId = "prod_2019_teams_seat", Nickname = "2019 Teams Seat (Monthly)", Amount = 250, Interval = "month" },
                        Quantity = 2
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "storage-gb-monthly", Metadata = new Dictionary<string, string> { ["isAddOn"] = "true" } },
                        Plan = new Plan { Id = "storage-gb-monthly", ProductId = "prod_storage_old", Nickname = "Additional Storage GB (Monthly)", Amount = 50, Interval = "month" },
                        Quantity = 3
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { Id = "teams-current-seat", UnitAmount = 400, ProductId = "prod_current_teams", Nickname = "Teams Organization Seat" },
                            Quantity = 7
                        },
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { Id = "storage-new", UnitAmount = 100, ProductId = "prod_storage_new", Nickname = "Additional Storage GB" },
                            Quantity = 3
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsMonthly2019)
            .Returns(new Teams2019Plan(false));

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — overage collapsed away, storage line preserved.
        Assert.Equal(2, result.Subscription!.Items.Count());
        Assert.DoesNotContain(result.Subscription.Items, i => i.PriceId == "teams-org-seat-monthly");
        Assert.Contains(result.Subscription.Items, i => i.ProductId == "prod_current_teams" && i.Quantity == 7);
        var storage = Assert.Single(result.Subscription.Items, i => i.PriceId == "storage-gb-monthly");
        Assert.Equal(3, storage.Quantity);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_NonPackagedMigrationSource_PreservesAllLineItems(
        SutProvider<StripePaymentService> sutProvider,
        Organization subscriber)
    {
        // Arrange — a Preserve-policy migration source (Teams 2020) is not a Packaged source, so the
        // overage collapse must not apply and every line item is preserved.
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";
        subscriber.PlanType = PlanType.TeamsMonthly2020;

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            ScheduleId = "sub_sched_test123",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(),
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "2020-teams-org-seat-monthly" },
                        Plan = new Plan { Id = "2020-teams-org-seat-monthly", ProductId = "prod_teams_2020", Nickname = "Teams Organization Seat (Monthly)", Amount = 400, Interval = "month" },
                        Quantity = 10
                    }
                ]
            }
        };

        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = DateTime.UtcNow.AddDays(-30) },
                new SubscriptionSchedulePhase
                {
                    StartDate = DateTime.UtcNow.AddDays(10),
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            Price = new Price { Id = "teams-current-seat", UnitAmount = 500, ProductId = "prod_teams_2020", Nickname = "Teams Organization Seat (Monthly)" },
                            Quantity = 10
                        }
                    ]
                }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(subscriber.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionScheduleAsync("sub_sched_test123", Arg.Any<SubscriptionScheduleGetOptions>())
            .Returns(schedule);

        // Act — a Preserve migration never triggers a plan fetch (ShouldCollapseSeatOverageLine is false).
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert — the single seat line is repriced by Phase 2 but not removed.
        var item = Assert.Single(result.Subscription!.Items);
        Assert.Equal("prod_teams_2020", item.ProductId);
        Assert.Equal(5.00m, item.Amount);
        Assert.Equal(10, item.Quantity);
    }
}
