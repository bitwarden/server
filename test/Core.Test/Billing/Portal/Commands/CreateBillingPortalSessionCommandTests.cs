using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Portal.Commands;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.BillingPortal;
using Xunit;

namespace Bit.Core.Test.Billing.Portal.Commands;

using static StripeConstants;

public class CreateBillingPortalSessionCommandTests
{
    private readonly ILogger<CreateBillingPortalSessionCommand> _logger = Substitute.For<ILogger<CreateBillingPortalSessionCommand>>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly CreateBillingPortalSessionCommand _command;
    private readonly User _user;

    public CreateBillingPortalSessionCommandTests()
    {
        _command = new CreateBillingPortalSessionCommand(_logger, _stripeAdapter);
        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };
    }

    [Fact]
    public async Task Run_WithValidUser_ReturnsPortalUrl()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var expectedUrl = "https://billing.stripe.com/session/test123";
        var session = new Session { Url = expectedUrl };
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns(session);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(expectedUrl, result.AsT0);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(
            Arg.Is<SessionCreateOptions>(o =>
                o.Customer == _user.GatewayCustomerId &&
                o.ReturnUrl == returnUrl));

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully created billing portal session") && o.ToString()!.Contains(_user.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WithoutGatewayCustomerId_ReturnsBadRequest()
    {
        // Arrange
        var userWithoutCustomerId = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            GatewayCustomerId = null
        };
        var returnUrl = "https://example.com/billing";

        // Act
        var result = await _command.Run(userWithoutCustomerId, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a Stripe customer ID.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("does not have a Stripe customer ID") && o.ToString()!.Contains(userWithoutCustomerId.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WithEmptyGatewayCustomerId_ReturnsBadRequest()
    {
        // Arrange
        var userWithEmptyCustomerId = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            GatewayCustomerId = string.Empty
        };
        var returnUrl = "https://example.com/billing";

        // Act
        var result = await _command.Run(userWithEmptyCustomerId, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a Stripe customer ID.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("does not have a Stripe customer ID") && o.ToString()!.Contains(userWithEmptyCustomerId.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WhenSessionIsNull_ReturnsConflict()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns((Session?)null);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT2);
        var conflict = result.AsT2;
        Assert.Equal("Unable to create billing portal session. Please contact support for assistance.", conflict.Response);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Fact]
    public async Task Run_WhenSessionUrlIsNull_ReturnsConflict()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };
        var session = new Session { Url = null };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns(session);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT2);
        var conflict = result.AsT2;
        Assert.Equal("Unable to create billing portal session. Please contact support for assistance.", conflict.Response);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Fact]
    public async Task Run_WhenStripeThrowsException_ReturnsUnhandled()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };
        var stripeException = new StripeException { StripeError = new StripeError { Code = "api_error" } };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Throws(stripeException);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.Equal(stripeException, unhandled.Exception);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Fact]
    public async Task Run_WithDifferentReturnUrls_UsesCorrectUrl()
    {
        // Arrange
        var returnUrl1 = "https://example.com/billing";
        var returnUrl2 = "https://different.com/account";
        var session = new Session { Url = "https://billing.stripe.com/session/test123" };
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns(session);

        // Act
        var result1 = await _command.Run(_user, returnUrl1);
        var result2 = await _command.Run(_user, returnUrl2);

        // Assert
        Assert.True(result1.IsT0);
        Assert.True(result2.IsT0);

        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(
            Arg.Is<SessionCreateOptions>(o => o.ReturnUrl == returnUrl1));
        await _stripeAdapter.Received(1).CreateBillingPortalSessionAsync(
            Arg.Is<SessionCreateOptions>(o => o.ReturnUrl == returnUrl2));
    }

    [Fact]
    public async Task Run_WithoutGatewaySubscriptionId_ReturnsBadRequest()
    {
        // Arrange
        var userWithoutSubscriptionId = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = null
        };
        var returnUrl = "https://example.com/billing";

        // Act
        var result = await _command.Run(userWithoutSubscriptionId, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a Premium subscription.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("does not have a subscription") && o.ToString()!.Contains(userWithoutSubscriptionId.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WithActiveSubscription_ReturnsPortalUrl()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var expectedUrl = "https://billing.stripe.com/session/test123";
        var session = new Session { Url = expectedUrl };
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Active };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns(session);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(expectedUrl, result.AsT0);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_WithPastDueSubscription_ReturnsPortalUrl()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var expectedUrl = "https://billing.stripe.com/session/test456";
        var session = new Session { Url = expectedUrl };
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.PastDue };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeAdapter.CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>())
            .Returns(session);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(expectedUrl, result.AsT0);

        await _stripeAdapter.Received(1).GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_WithCanceledSubscription_ReturnsBadRequest()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Canceled };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Your subscription cannot be managed in its current status.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("not eligible for portal access") && o.ToString()!.Contains(_user.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WithIncompleteSubscription_ReturnsBadRequest()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var subscription = new Subscription { Id = _user.GatewaySubscriptionId, Status = SubscriptionStatus.Incomplete };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Your subscription cannot be managed in its current status.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Fact]
    public async Task Run_WhenSubscriptionFetchFails_ReturnsBadRequest()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";
        var stripeException = new StripeException { StripeError = new StripeError { Code = "resource_missing" } };

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Throws(stripeException);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Unable to verify subscription status.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to fetch subscription") && o.ToString()!.Contains(_user.Id.ToString())),
            stripeException,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_WhenSubscriptionIsNull_ReturnsBadRequest()
    {
        // Arrange
        var returnUrl = "https://example.com/billing";

        _stripeAdapter.GetSubscriptionAsync(_user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns((Subscription?)null);

        // Act
        var result = await _command.Run(_user, returnUrl);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User subscription not found.", badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateBillingPortalSessionAsync(Arg.Any<SessionCreateOptions>());

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("was not found") && o.ToString()!.Contains(_user.Id.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
