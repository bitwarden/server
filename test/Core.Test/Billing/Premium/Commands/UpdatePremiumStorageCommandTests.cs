using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class UpdatePremiumStorageCommandTests
{
    private readonly IBraintreeService _braintreeService = Substitute.For<IBraintreeService>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly UpdatePremiumStorageCommand _command;

    public UpdatePremiumStorageCommandTests()
    {
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = "price_premium", Provided = 1 },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = "price_storage", Provided = 1 }
        };
        _pricingClient.ListPremiumPlans().Returns([premiumPlan]);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        _command = new UpdatePremiumStorageCommand(
            _braintreeService,
            _stripeAdapter,
            _userService,
            _pricingClient,
            Substitute.For<ILogger<UpdatePremiumStorageCommand>>());
    }

    private Subscription CreateMockSubscription(string subscriptionId, int? storageQuantity = null, bool isPayPal = false)
    {
        var items = new List<SubscriptionItem>
        {
            // Always add the seat item
            new()
            {
                Id = "si_seat",
                Price = new Price { Id = "price_premium" },
                Quantity = 1
            }
        };

        // Add storage item if quantity is provided
        if (storageQuantity is > 0)
        {
            items.Add(new SubscriptionItem
            {
                Id = "si_storage",
                Price = new Price { Id = "price_storage" },
                Quantity = storageQuantity.Value
            });
        }

        var customer = new Customer
        {
            Id = "cus_123",
            Metadata = isPayPal ? new Dictionary<string, string> { { MetadataKeys.BraintreeCustomerId, "braintree_123" } } : new Dictionary<string, string>()
        };

        return new Subscription
        {
            Id = subscriptionId,
            CustomerId = "cus_123",
            Customer = customer,
            Items = new StripeList<SubscriptionItem>
            {
                Data = items
            }
        };
    }

    [Theory, BitAutoData]
    public async Task Run_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_NegativeStorage_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, -5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Additional storage cannot be negative.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_StorageExceedsMaximum_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 100);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Maximum storage is 100 GB.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_NoMaxStorageGb_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = null;

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User has no access to storage.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_StorageExceedsCurrentUsage_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 5L * 1024 * 1024 * 1024; // 5 GB currently used
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("You are currently using", badRequest.Response);
        Assert.Contains("Delete some stored data first", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_SameStorageAmount_Idempotent(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 4);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was fetched but NOT updated
        await _stripeAdapter.Received(1).GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _userService.DidNotReceive().SaveUserAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 9);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Quantity == 9 &&
                opts.ProrationBehavior == "always_invoice"));

        // Verify user was saved
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_AddStorageFromZero_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 1;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123");
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 9);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated with new storage item
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Price == "price_storage" &&
                opts.Items[0].Quantity == 9));

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_DecreaseStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 2);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Quantity == 2));

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 3));
    }

    [Theory, BitAutoData]
    public async Task Run_RemoveAllAdditionalStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 0);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription item was deleted
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Deleted == true));

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 1));
    }

    [Theory, BitAutoData]
    public async Task Run_MaximumStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        var result = await _command.Run(user, 99);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items[0].Quantity == 99));

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 100));
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_PayPal_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4, isPayPal: true);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var draftInvoice = new Invoice { Id = "in_draft" };
        _stripeAdapter.CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice
        {
            Id = "in_finalized",
            Customer = new Customer { Id = "cus_123" }
        };
        _stripeAdapter.FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>()).Returns(finalizedInvoice);

        // Act
        var result = await _command.Run(user, 9);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated with CreateProrations
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Quantity == 9 &&
                opts.ProrationBehavior == "create_prorations"));

        // Verify draft invoice was created
        await _stripeAdapter.Received(1).CreateInvoiceAsync(
            Arg.Is<InvoiceCreateOptions>(opts =>
                opts.Customer == "cus_123" &&
                opts.Subscription == "sub_123" &&
                opts.AutoAdvance == false &&
                opts.CollectionMethod == "charge_automatically"));

        // Verify invoice was finalized
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync(
            "in_draft",
            Arg.Is<InvoiceFinalizeOptions>(opts =>
                opts.AutoAdvance == false &&
                opts.Expand.Contains("customer")));

        // Verify Braintree payment was processed
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), finalizedInvoice);

        // Verify user was saved
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_AddStorageFromZero_PayPal_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 1;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", isPayPal: true);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var draftInvoice = new Invoice { Id = "in_draft" };
        _stripeAdapter.CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice
        {
            Id = "in_finalized",
            Customer = new Customer { Id = "cus_123" }
        };
        _stripeAdapter.FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>()).Returns(finalizedInvoice);

        // Act
        var result = await _command.Run(user, 9);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated with new storage item
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Price == "price_storage" &&
                opts.Items[0].Quantity == 9 &&
                opts.ProrationBehavior == "create_prorations"));

        // Verify invoice creation and payment flow
        await _stripeAdapter.Received(1).CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>());
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), finalizedInvoice);

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_DecreaseStorage_PayPal_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9, isPayPal: true);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var draftInvoice = new Invoice { Id = "in_draft" };
        _stripeAdapter.CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice
        {
            Id = "in_finalized",
            Customer = new Customer { Id = "cus_123" }
        };
        _stripeAdapter.FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>()).Returns(finalizedInvoice);

        // Act
        var result = await _command.Run(user, 2);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was updated
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Quantity == 2 &&
                opts.ProrationBehavior == "create_prorations"));

        // Verify invoice creation and payment flow
        await _stripeAdapter.Received(1).CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>());
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), finalizedInvoice);

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 3));
    }

    [Theory, BitAutoData]
    public async Task Run_RemoveAllAdditionalStorage_PayPal_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9, isPayPal: true);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var draftInvoice = new Invoice { Id = "in_draft" };
        _stripeAdapter.CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice
        {
            Id = "in_finalized",
            Customer = new Customer { Id = "cus_123" }
        };
        _stripeAdapter.FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>()).Returns(finalizedInvoice);

        // Act
        var result = await _command.Run(user, 0);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription item was deleted
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 &&
                opts.Items[0].Id == "si_storage" &&
                opts.Items[0].Deleted == true &&
                opts.ProrationBehavior == "create_prorations"));

        // Verify invoice creation and payment flow
        await _stripeAdapter.Received(1).CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>());
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), finalizedInvoice);

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 1));
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_WithSchedule_UpdatesBothPhases(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: true, storageQuantity: 4);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(user, 9);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.ProrationBehavior == "always_invoice" &&
                opts.Phases.Count == 2 &&
                opts.Phases[0].ProrationBehavior == "none" &&
                opts.Phases[0].Items.Any(i => i.Price == "price_storage" && i.Quantity == 9) &&
                opts.Phases[1].Items.Any(i => i.Price == "price_storage" && i.Quantity == 9)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_AddStorageFromZero_WithSchedule_AddsToBothPhases(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 1;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123");
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: false);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(user, 5);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == "price_storage" && i.Quantity == 5) &&
                opts.Phases[1].Items.Any(i => i.Price == "price_storage" && i.Quantity == 5)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 6));
    }

    [Theory, BitAutoData]
    public async Task Run_RemoveStorage_WithSchedule_RemovesFromBothPhases(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.Storage = 500L * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 9);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: true, storageQuantity: 9);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(user, 0);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.All(i => i.Price != "price_storage") &&
                opts.Phases[1].Items.All(i => i.Price != "price_storage")));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 1));
    }

    [Theory, BitAutoData]
    public async Task Run_WithSchedule_PreservesExistingItems(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: true, storageQuantity: 4);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(user, 9);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.EndBehavior == SubscriptionScheduleEndBehavior.Release &&
                opts.Phases[0].Items.Any(i => i.Price == "price_premium" && i.Quantity == 1) &&
                opts.Phases[1].Items.Any(i => i.Price == "price_premium_new" && i.Quantity == 1) &&
                opts.Phases[1].Discounts != null && opts.Phases[1].Discounts.Any(d => d.Coupon == "coupon_123")));
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_WithSchedule_PayPal_UsesProrationsAndBraintree(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4, isPayPal: true);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: true, storageQuantity: 4);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var draftInvoice = new Invoice { Id = "in_draft" };
        _stripeAdapter.CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice
        {
            Id = "in_finalized",
            Customer = new Customer { Id = "cus_123" }
        };
        _stripeAdapter.FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>()).Returns(finalizedInvoice);

        var result = await _command.Run(user, 9);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.ProrationBehavior == "create_prorations" &&
                opts.Phases[0].ProrationBehavior == "none" &&
                opts.Phases[0].Items.Any(i => i.Price == "price_storage" && i.Quantity == 9) &&
                opts.Phases[1].Items.Any(i => i.Price == "price_storage" && i.Quantity == 9)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _stripeAdapter.Received(1).CreateInvoiceAsync(Arg.Any<InvoiceCreateOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("in_draft", Arg.Any<InvoiceFinalizeOptions>());
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), finalizedInvoice);
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_WithSinglePhaseSchedule_UpdatesOnlyPhase1(User user)
    {
        user.Premium = true;
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024;
        user.GatewaySubscriptionId = "sub_123";

        var subscription = CreateMockSubscription("sub_123", 4);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var schedule = CreateMockSchedule("sub_123", hasStorage: true, storageQuantity: 4, singlePhase: true);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var result = await _command.Run(user, 9);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.EndBehavior == SubscriptionScheduleEndBehavior.Cancel &&
                opts.Phases.Count == 1 &&
                opts.Phases[0].Items.Any(i => i.Price == "price_storage" && i.Quantity == 9) &&
                opts.Phases[0].Items.Any(i => i.Price == "price_premium" && i.Quantity == 1)));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u => u.MaxStorageGb == 10));
    }

    private static SubscriptionSchedule CreateMockSchedule(
        string subscriptionId,
        bool hasStorage,
        int storageQuantity = 0,
        bool singlePhase = false)
    {
        var phase1Items = new List<SubscriptionSchedulePhaseItem>
        {
            new() { PriceId = "price_premium", Quantity = 1 }
        };

        if (hasStorage)
        {
            phase1Items.Add(new SubscriptionSchedulePhaseItem { PriceId = "price_storage", Quantity = storageQuantity });
        }

        var phases = new List<SubscriptionSchedulePhase>
        {
            new()
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                Items = phase1Items,
                ProrationBehavior = ProrationBehavior.None
            }
        };

        if (!singlePhase)
        {
            var phase2Items = new List<SubscriptionSchedulePhaseItem>
            {
                new() { PriceId = "price_premium_new", Quantity = 1 }
            };

            if (hasStorage)
            {
                phase2Items.Add(new SubscriptionSchedulePhaseItem { PriceId = "price_storage", Quantity = storageQuantity });
            }

            phases.Add(new SubscriptionSchedulePhase
            {
                StartDate = DateTime.UtcNow.AddYears(1),
                Items = phase2Items,
                Discounts =
                [
                    new SubscriptionSchedulePhaseDiscount { CouponId = "coupon_123" }
                ],
                ProrationBehavior = ProrationBehavior.None
            });
        }

        return new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscriptionId,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = singlePhase
                ? SubscriptionScheduleEndBehavior.Cancel
                : SubscriptionScheduleEndBehavior.Release,
            Phases = phases
        };
    }
}
