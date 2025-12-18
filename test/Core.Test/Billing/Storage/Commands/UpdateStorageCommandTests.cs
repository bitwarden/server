using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Storage.Commands;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Storage.Commands;

public class UpdateStorageCommandTests
{
    private readonly IStripePaymentService _paymentService = Substitute.For<IStripePaymentService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly UpdateStorageCommand _command;

    public UpdateStorageCommandTests()
    {
        // Setup default premium plan with standard pricing
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = "price_premium", Provided = 1 },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = "price_storage", Provided = 1 }
        };
        _pricingClient.GetAvailablePremiumPlan().Returns(premiumPlan);

        _command = new UpdateStorageCommand(
            _paymentService,
            _userService,
            _pricingClient,
            Substitute.For<ILogger<UpdateStorageCommand>>());
    }

    [Fact]
    public async Task Run_NullUser_ReturnsBadRequest()
    {
        // Act
        var result = await _command.Run(null!, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User not found.", badRequest.Response);
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
    public async Task Run_NoGatewayCustomerId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = null;

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("No payment method found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_NoGatewaySubscriptionId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = null;

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("No subscription found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_StorageLessThanBase_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;

        // Act - Try to set storage to 0 (less than base of 1)
        var result = await _command.Run(user, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Storage cannot be less than the base amount of 1 GB.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_StorageExceedsMaximum_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;

        // Act - Try to set storage to 101 GB (exceeds max of 100)
        var result = await _command.Run(user, 101);

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
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = null;

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("No access to storage.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_StorageExceedsCurrentUsage_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 10;
        user.Storage = 5L * 1024 * 1024 * 1024; // 5 GB currently used

        // Act - Try to reduce to 2 GB (less than current usage)
        var result = await _command.Run(user, 2);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("You are currently using", badRequest.Response);
        Assert.Contains("Delete some stored data first", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_IncreaseStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024; // 2 GB currently used

        var expectedPaymentIntentSecret = "pi_secret_123";
        _paymentService.AdjustStorageAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<int>(storage => storage == 9), // 10 - 1 (base) = 9 additional
            Arg.Is<string>(priceId => priceId == "price_storage"))
            .Returns(expectedPaymentIntentSecret);

        // Act - Increase storage from 5 GB to 10 GB
        var result = await _command.Run(user, 10);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Equal(expectedPaymentIntentSecret, paymentSecret);

        // Verify the user was saved with updated storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 10));
    }

    [Theory, BitAutoData]
    public async Task Run_DecreaseStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 10;
        user.Storage = 2L * 1024 * 1024 * 1024; // 2 GB currently used

        var expectedPaymentIntentSecret = "pi_secret_456";
        _paymentService.AdjustStorageAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<int>(storage => storage == 2), // 3 - 1 (base) = 2 additional
            Arg.Is<string>(priceId => priceId == "price_storage"))
            .Returns(expectedPaymentIntentSecret);

        // Act - Decrease storage from 10 GB to 3 GB
        var result = await _command.Run(user, 3);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Equal(expectedPaymentIntentSecret, paymentSecret);

        // Verify the user was saved with updated storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 3));
    }

    [Theory, BitAutoData]
    public async Task Run_SetToBaseStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 10;
        user.Storage = 500L * 1024 * 1024; // 500 MB currently used

        var expectedPaymentIntentSecret = "pi_secret_789";
        _paymentService.AdjustStorageAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<int>(storage => storage == 0), // 1 - 1 (base) = 0 additional
            Arg.Is<string>(priceId => priceId == "price_storage"))
            .Returns(expectedPaymentIntentSecret);

        // Act - Set storage to base amount of 1 GB
        var result = await _command.Run(user, 1);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Equal(expectedPaymentIntentSecret, paymentSecret);

        // Verify the user was saved with updated storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 1));
    }

    [Theory, BitAutoData]
    public async Task Run_SameStorageAmount_Idempotent(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024; // 2 GB currently used

        // Act - Set storage to same amount (5 GB) - should return null without calling payment service
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Null(paymentSecret); // Idempotent - no payment needed when storage is unchanged

        // Verify the payment service was NOT called (optimization for idempotent operation)
        await _paymentService.DidNotReceive().AdjustStorageAsync(
            Arg.Any<User>(),
            Arg.Any<int>(),
            Arg.Any<string>());

        // Verify the user was NOT saved (no changes made)
        await _userService.DidNotReceive().SaveUserAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task Run_MaximumStorage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024; // 2 GB currently used

        var expectedPaymentIntentSecret = "pi_secret_max";
        _paymentService.AdjustStorageAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<int>(storage => storage == 99), // 100 - 1 (base) = 99 additional
            Arg.Is<string>(priceId => priceId == "price_storage"))
            .Returns(expectedPaymentIntentSecret);

        // Act - Set storage to maximum of 100 GB
        var result = await _command.Run(user, 100);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Equal(expectedPaymentIntentSecret, paymentSecret);

        // Verify the user was saved with maximum storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 100));
    }

    [Theory, BitAutoData]
    public async Task Run_NegativeStorage_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;

        // Act - Try to set storage to negative value
        var result = await _command.Run(user, -5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Storage cannot be less than the base amount of 1 GB.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_WhitespaceGatewayCustomerId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "   "; // Whitespace only
        user.GatewaySubscriptionId = "sub_123";

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("No payment method found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_EmptyGatewaySubscriptionId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = ""; // Empty string

        // Act
        var result = await _command.Run(user, 5);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("No subscription found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_PaymentServiceReturnsNull_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 5;
        user.Storage = 2L * 1024 * 1024 * 1024; // 2 GB currently used

        // Payment service returns null (no payment intent needed)
        _paymentService.AdjustStorageAsync(
            Arg.Any<User>(),
            Arg.Any<int>(),
            Arg.Any<string>())
            .Returns((string?)null);

        // Act - Increase storage from 5 GB to 6 GB
        var result = await _command.Run(user, 6);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        // Payment service can return null (no payment intent needed) or empty string
        Assert.True(string.IsNullOrEmpty(paymentSecret));

        // Verify the user was still saved with updated storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 6));
    }

    [Theory, BitAutoData]
    public async Task Run_DecreaseToExactlyCurrentUsage_Success(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "cus_123";
        user.GatewaySubscriptionId = "sub_123";
        user.MaxStorageGb = 10;
        user.Storage = 3L * 1024 * 1024 * 1024; // Exactly 3 GB currently used

        var expectedPaymentIntentSecret = "pi_secret_exact";
        _paymentService.AdjustStorageAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<int>(storage => storage == 2), // 3 - 1 (base) = 2 additional
            Arg.Is<string>(priceId => priceId == "price_storage"))
            .Returns(expectedPaymentIntentSecret);

        // Act - Decrease storage to exactly match current usage (3 GB)
        var result = await _command.Run(user, 3);

        // Assert
        Assert.True(result.IsT0);
        var paymentSecret = result.AsT0;
        Assert.Equal(expectedPaymentIntentSecret, paymentSecret);

        // Verify the user was saved with updated storage
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Id == user.Id &&
            u.MaxStorageGb == 3));
    }
}
