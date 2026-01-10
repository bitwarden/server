using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class UpdatePremiumStorageCommandTests
{
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

        _command = new UpdatePremiumStorageCommand(
            _stripeAdapter,
            _userService,
            _pricingClient,
            Substitute.For<ILogger<UpdatePremiumStorageCommand>>());
    }

    private Subscription CreateMockSubscription(string subscriptionId, int? storageQuantity = null)
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

        return new Subscription
        {
            Id = subscriptionId,
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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

        // Act
        var result = await _command.Run(user, 4);

        // Assert
        Assert.True(result.IsT0);

        // Verify subscription was fetched but NOT updated
        await _stripeAdapter.Received(1).GetSubscriptionAsync("sub_123");
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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(subscription);

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
}
