using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using Braintree.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

using BraintreeService = Bit.Core.Services.Implementations.BraintreeService;
using Customer = Stripe.Customer;

namespace Bit.Core.Test.Services.Implementations;

public class BraintreeServiceTests
{
    private readonly ICustomerGateway _customerGateway;
    private readonly BraintreeService _sut;

    public BraintreeServiceTests()
    {
        var braintreeGateway = Substitute.For<IBraintreeGateway>();
        _customerGateway = Substitute.For<ICustomerGateway>();
        braintreeGateway.Customer.Returns(_customerGateway);

        var globalSettings = Substitute.For<IGlobalSettings>();
        var logger = Substitute.For<ILogger<BraintreeService>>();
        var mailService = Substitute.For<IMailService>();
        var stripeAdapter = Substitute.For<IStripeAdapter>();

        _sut = new BraintreeService(
            braintreeGateway,
            globalSettings,
            logger,
            mailService,
            stripeAdapter);
    }

    #region GetCustomer

    [Fact]
    public async Task GetCustomer_NoBraintreeCustomerIdInMetadata_ReturnsNull()
    {
        // Arrange
        var stripeCustomer = new Customer
        {
            Id = "cus_123",
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await _sut.GetCustomer(stripeCustomer);

        // Assert
        Assert.Null(result);
        await _customerGateway.DidNotReceiveWithAnyArgs().FindAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetCustomer_BraintreeCustomerFound_ReturnsCustomer()
    {
        // Arrange
        const string braintreeCustomerId = "bt_customer_123";

        var stripeCustomer = new Customer
        {
            Id = "cus_123",
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.BraintreeCustomerId] = braintreeCustomerId
            }
        };

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        _customerGateway
            .FindAsync(braintreeCustomerId)
            .Returns(braintreeCustomer);

        // Act
        var result = await _sut.GetCustomer(stripeCustomer);

        // Assert
        Assert.NotNull(result);
        Assert.Same(braintreeCustomer, result);
        await _customerGateway.Received(1).FindAsync(braintreeCustomerId);
    }

    [Fact]
    public async Task GetCustomer_BraintreeCustomerNotFound_LogsWarningAndReturnsNull()
    {
        // Arrange
        const string braintreeCustomerId = "bt_non_existent_customer";

        var stripeCustomer = new Customer
        {
            Id = "cus_123",
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.BraintreeCustomerId] = braintreeCustomerId
            }
        };

        _customerGateway
            .FindAsync(braintreeCustomerId)
            .Returns<Braintree.Customer>(_ => throw new NotFoundException());

        // Act
        var result = await _sut.GetCustomer(stripeCustomer);

        // Assert
        Assert.Null(result);
        await _customerGateway.Received(1).FindAsync(braintreeCustomerId);
    }

    #endregion
}
