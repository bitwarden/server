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

    #region AdjustSubscription — CompleteSubscriptionUpdate tax exempt alignment

    [Theory, BitAutoData]
    public async Task AdjustSubscription_WhenNonDirectTaxCountry_SetsReverseCharge(
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
                TaxExempt = TaxExempt.None
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

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt == TaxExempt.Reverse));
    }

    [Theory, BitAutoData]
    public async Task AdjustSubscription_WhenUSWithManualReverse_CorrectsTaxExemptToNone(
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
                Address = new Address { Country = "US" },
                TaxExempt = TaxExempt.Reverse
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

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt == TaxExempt.None));
    }

    [Theory, BitAutoData]
    public async Task AdjustSubscription_WhenSwissWithReverse_CorrectsTaxExemptToNone(
        SutProvider<StripePaymentService> sutProvider,
        Organization organization)
    {
        // CH is a direct-tax country — "reverse" is not preserved; it should be corrected to "none".
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
                Address = new Address { Country = "CH" },
                TaxExempt = TaxExempt.Reverse
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

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(options => options.TaxExempt == TaxExempt.None));
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

    #endregion
}
