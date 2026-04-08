using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Identity.Billing.Controller;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.Billing.Controller;

public class AccountsControllerTests
{
    private readonly ISendTrialInitiationEmailForRegistrationCommand _sendTrialInitiationEmailForRegistrationCommand;
    private readonly AccountsController _sut;

    public AccountsControllerTests()
    {
        _sendTrialInitiationEmailForRegistrationCommand = Substitute.For<ISendTrialInitiationEmailForRegistrationCommand>();
        _sut = new AccountsController(_sendTrialInitiationEmailForRegistrationCommand);
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_PaymentOptionalTrue_TrialLengthZero_ReturnsBadRequest(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = 0,
            PaymentOptional = true
        };

        // Act
        var result = await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        Assert.Equal("Payment cannot be optional when trial length is zero.", messageProperty.GetValue(value));

        await _sendTrialInitiationEmailForRegistrationCommand.DidNotReceive()
            .Handle(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<ProductTierType>(),
                Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_PaymentOptionalTrue_TrialLengthNonZero_ReturnsSuccess(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        string token)
    {
        // Arrange
        var trialLength = 7;
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = trialLength,
            PaymentOptional = true
        };

        _sendTrialInitiationEmailForRegistrationCommand
            .Handle(email, name, receiveMarketingEmails, productTier, products, trialLength, true)
            .Returns(token);

        // Act
        var result = await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(token, okResult.Value);

        await _sendTrialInitiationEmailForRegistrationCommand.Received(1)
            .Handle(email, name, receiveMarketingEmails, productTier, products, trialLength, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_PaymentOptionalFalse_TrialLengthZero_ReturnsSuccess(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = 0,
            PaymentOptional = false
        };

        _sendTrialInitiationEmailForRegistrationCommand
            .Handle(email, name, receiveMarketingEmails, productTier, products, 0, false)
            .Returns((string?)null);

        // Act
        var result = await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);

        await _sendTrialInitiationEmailForRegistrationCommand.Received(1)
            .Handle(email, name, receiveMarketingEmails, productTier, products, 0, false);
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_TrialLengthNull_DefaultsToSeven(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        string token)
    {
        // Arrange
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = null,
            PaymentOptional = false
        };

        _sendTrialInitiationEmailForRegistrationCommand
            .Handle(email, name, receiveMarketingEmails, productTier, products, 7, false)
            .Returns(token);

        // Act
        var result = await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        await _sendTrialInitiationEmailForRegistrationCommand.Received(1)
            .Handle(email, name, receiveMarketingEmails, productTier, products, 7, false);
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_CommandReturnsNull_ReturnsNoContent(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = 7,
            PaymentOptional = false
        };

        _sendTrialInitiationEmailForRegistrationCommand
            .Handle(email, name, receiveMarketingEmails, productTier, products, 7, false)
            .Returns((string?)null);

        // Act
        var result = await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostTrialInitiationSendVerificationEmailAsync_PaymentOptionalTrue_PassedToCommand(
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        var model = new TrialSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            ProductTier = productTier,
            Products = products,
            TrialLength = 14,
            PaymentOptional = true
        };

        _sendTrialInitiationEmailForRegistrationCommand
            .Handle(email, name, receiveMarketingEmails, productTier, products, 14, true)
            .Returns((string?)null);

        // Act
        await _sut.PostTrialInitiationSendVerificationEmailAsync(model);

        // Assert
        await _sendTrialInitiationEmailForRegistrationCommand.Received(1)
            .Handle(email, name, receiveMarketingEmails, productTier, products, 14, true);
    }
}
