using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class CreatePremiumCloudHostedSubscriptionCommandTests
{
    private readonly IPremiumUserBillingService _premiumUserBillingService = Substitute.For<IPremiumUserBillingService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly CreatePremiumCloudHostedSubscriptionCommand _command;

    public CreatePremiumCloudHostedSubscriptionCommandTests()
    {
        _command = new CreatePremiumCloudHostedSubscriptionCommand(
            _premiumUserBillingService,
            _userService,
            _paymentService,
            _pushNotificationService,
            Substitute.For<ILogger<CreatePremiumCloudHostedSubscriptionCommand>>());
    }

    [Fact]
    public async Task Run_UserAlreadyPremium_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = true
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Already a premium user.", badRequest.Response);
    }

    [Fact]
    public async Task Run_NegativeStorageAmount_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, -1);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("You can't subtract storage!", badRequest.Response);
    }

    [Theory]
    [InlineData(TokenizablePaymentMethodType.BankAccount)]
    [InlineData(TokenizablePaymentMethodType.Card)]
    [InlineData(TokenizablePaymentMethodType.PayPal)]
    public async Task Run_ValidPaymentMethodTypes_Success(TokenizablePaymentMethodType paymentMethodType)
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "test@example.com"
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = paymentMethodType,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _premiumUserBillingService.Received(1).Finalize(Arg.Any<Bit.Core.Billing.Models.Sales.PremiumUserSale>());
    }

    [Fact]
    public async Task Run_ValidRequestWithAdditionalStorage_Success()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "test@example.com"
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        const short additionalStorage = 2;

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, additionalStorage);

        // Assert
        Assert.True(result.IsT0);

        // Verify user was updated correctly
        Assert.True(user.Premium);
        Assert.Equal((short)(1 + additionalStorage), user.MaxStorageGb);
        Assert.NotNull(user.LicenseKey);
        Assert.Equal(20, user.LicenseKey.Length);
        Assert.NotEqual(default, user.RevisionDate);

        // Verify services were called
        await _premiumUserBillingService.Received(1).Finalize(Arg.Any<Bit.Core.Billing.Models.Sales.PremiumUserSale>());
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Fact]
    public async Task Run_UserServiceThrowsException_CancelsChargesAndRethrows()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "test@example.com"
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var expectedException = new Exception("User service error");
        _userService.When(x => x.SaveUserAsync(user)).Do(x => throw expectedException);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT3);
        // Verify payment cancellation was attempted
        await _paymentService.Received(1).CancelAndRecoverChargesAsync(user);
    }

    [Fact]
    public async Task Run_PushServiceThrowsException_CancelsChargesAndRethrows()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "test@example.com"
        };

        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "token_123"
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var expectedException = new Exception("Push service error");
        _pushNotificationService.When(x => x.PushSyncVaultAsync(user.Id)).Do(x => throw expectedException);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT3);
        // Verify payment cancellation was attempted
        await _paymentService.Received(1).CancelAndRecoverChargesAsync(user);
    }
}
