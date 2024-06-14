using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Billing.Test.Utilities;
using Bit.Core.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class StripeEventServiceTests
{
    private readonly IStripeFacade _stripeFacade;
    private readonly StripeEventService _stripeEventService;

    public StripeEventServiceTests()
    {
        var globalSettings = new GlobalSettings();
        var baseServiceUriSettings = new GlobalSettings.BaseServiceUriSettings(globalSettings) { CloudRegion = "US" };
        globalSettings.BaseServiceUri = baseServiceUriSettings;

        _stripeFacade = Substitute.For<IStripeFacade>();
        _stripeEventService = new StripeEventService(globalSettings, Substitute.For<ILogger<StripeEventService>>(), _stripeFacade);
    }

    #region GetCharge
    [Fact]
    public async Task GetCharge_EventNotChargeRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceCreated);

        // Act
        var function = async () => await _stripeEventService.GetCharge(stripeEvent);

        // Assert
        await function
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Charge)}'");

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCharge(
            Arg.Any<string>(),
            Arg.Any<ChargeGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCharge_NotFresh_ReturnsEventCharge()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.ChargeSucceeded);

        // Act
        var charge = await _stripeEventService.GetCharge(stripeEvent);

        // Assert
        charge.Should().BeEquivalentTo(stripeEvent.Data.Object as Charge);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCharge(
            Arg.Any<string>(),
            Arg.Any<ChargeGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCharge_Fresh_Expand_ReturnsAPICharge()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.ChargeSucceeded);

        var eventCharge = stripeEvent.Data.Object as Charge;

        var apiCharge = Copy(eventCharge);

        var expand = new List<string> { "customer" };

        _stripeFacade.GetCharge(
                apiCharge.Id,
                Arg.Is<ChargeGetOptions>(options => options.Expand == expand))
            .Returns(apiCharge);

        // Act
        var charge = await _stripeEventService.GetCharge(stripeEvent, true, expand);

        // Assert
        charge.Should().Be(apiCharge);
        charge.Should().NotBeSameAs(eventCharge);

        await _stripeFacade.Received().GetCharge(
            apiCharge.Id,
            Arg.Is<ChargeGetOptions>(options => options.Expand == expand),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    #region GetCustomer
    [Fact]
    public async Task GetCustomer_EventNotCustomerRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceCreated);

        // Act
        var function = async () => await _stripeEventService.GetCustomer(stripeEvent);

        // Assert
        await function
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Customer)}'");

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCustomer(
            Arg.Any<string>(),
            Arg.Any<CustomerGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCustomer_NotFresh_ReturnsEventCustomer()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        // Act
        var customer = await _stripeEventService.GetCustomer(stripeEvent);

        // Assert
        customer.Should().BeEquivalentTo(stripeEvent.Data.Object as Customer);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCustomer(
            Arg.Any<string>(),
            Arg.Any<CustomerGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCustomer_Fresh_Expand_ReturnsAPICustomer()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        var eventCustomer = stripeEvent.Data.Object as Customer;

        var apiCustomer = Copy(eventCustomer);

        var expand = new List<string> { "subscriptions" };

        _stripeFacade.GetCustomer(
                apiCustomer.Id,
                Arg.Is<CustomerGetOptions>(options => options.Expand == expand))
            .Returns(apiCustomer);

        // Act
        var customer = await _stripeEventService.GetCustomer(stripeEvent, true, expand);

        // Assert
        customer.Should().Be(apiCustomer);
        customer.Should().NotBeSameAs(eventCustomer);

        await _stripeFacade.Received().GetCustomer(
            apiCustomer.Id,
            Arg.Is<CustomerGetOptions>(options => options.Expand == expand),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    #region GetInvoice
    [Fact]
    public async Task GetInvoice_EventNotInvoiceRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        // Act
        var function = async () => await _stripeEventService.GetInvoice(stripeEvent);

        // Assert
        await function
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Invoice)}'");

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetInvoice(
            Arg.Any<string>(),
            Arg.Any<InvoiceGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInvoice_NotFresh_ReturnsEventInvoice()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceCreated);

        // Act
        var invoice = await _stripeEventService.GetInvoice(stripeEvent);

        // Assert
        invoice.Should().BeEquivalentTo(stripeEvent.Data.Object as Invoice);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetInvoice(
            Arg.Any<string>(),
            Arg.Any<InvoiceGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInvoice_Fresh_Expand_ReturnsAPIInvoice()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceCreated);

        var eventInvoice = stripeEvent.Data.Object as Invoice;

        var apiInvoice = Copy(eventInvoice);

        var expand = new List<string> { "customer" };

        _stripeFacade.GetInvoice(
                apiInvoice.Id,
                Arg.Is<InvoiceGetOptions>(options => options.Expand == expand))
            .Returns(apiInvoice);

        // Act
        var invoice = await _stripeEventService.GetInvoice(stripeEvent, true, expand);

        // Assert
        invoice.Should().Be(apiInvoice);
        invoice.Should().NotBeSameAs(eventInvoice);

        await _stripeFacade.Received().GetInvoice(
            apiInvoice.Id,
            Arg.Is<InvoiceGetOptions>(options => options.Expand == expand),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    #region GetPaymentMethod
    [Fact]
    public async Task GetPaymentMethod_EventNotPaymentMethodRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        // Act
        var function = async () => await _stripeEventService.GetPaymentMethod(stripeEvent);

        // Assert
        await function
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(PaymentMethod)}'");

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentMethod(
            Arg.Any<string>(),
            Arg.Any<PaymentMethodGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPaymentMethod_NotFresh_ReturnsEventPaymentMethod()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.PaymentMethodAttached);

        // Act
        var paymentMethod = await _stripeEventService.GetPaymentMethod(stripeEvent);

        // Assert
        paymentMethod.Should().BeEquivalentTo(stripeEvent.Data.Object as PaymentMethod);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentMethod(
            Arg.Any<string>(),
            Arg.Any<PaymentMethodGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPaymentMethod_Fresh_Expand_ReturnsAPIPaymentMethod()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.PaymentMethodAttached);

        var eventPaymentMethod = stripeEvent.Data.Object as PaymentMethod;

        var apiPaymentMethod = Copy(eventPaymentMethod);

        var expand = new List<string> { "customer" };

        _stripeFacade.GetPaymentMethod(
                apiPaymentMethod.Id,
                Arg.Is<PaymentMethodGetOptions>(options => options.Expand == expand))
            .Returns(apiPaymentMethod);

        // Act
        var paymentMethod = await _stripeEventService.GetPaymentMethod(stripeEvent, true, expand);

        // Assert
        paymentMethod.Should().Be(apiPaymentMethod);
        paymentMethod.Should().NotBeSameAs(eventPaymentMethod);

        await _stripeFacade.Received().GetPaymentMethod(
            apiPaymentMethod.Id,
            Arg.Is<PaymentMethodGetOptions>(options => options.Expand == expand),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    #region GetSubscription
    [Fact]
    public async Task GetSubscription_EventNotSubscriptionRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        // Act
        var function = async () => await _stripeEventService.GetSubscription(stripeEvent);

        // Assert
        await function
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Subscription)}'");

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(
            Arg.Any<string>(),
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubscription_NotFresh_ReturnsEventSubscription()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        // Act
        var subscription = await _stripeEventService.GetSubscription(stripeEvent);

        // Assert
        subscription.Should().BeEquivalentTo(stripeEvent.Data.Object as Subscription);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(
            Arg.Any<string>(),
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubscription_Fresh_Expand_ReturnsAPISubscription()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        var eventSubscription = stripeEvent.Data.Object as Subscription;

        var apiSubscription = Copy(eventSubscription);

        var expand = new List<string> { "customer" };

        _stripeFacade.GetSubscription(
                apiSubscription.Id,
                Arg.Is<SubscriptionGetOptions>(options => options.Expand == expand))
            .Returns(apiSubscription);

        // Act
        var subscription = await _stripeEventService.GetSubscription(stripeEvent, true, expand);

        // Assert
        subscription.Should().Be(apiSubscription);
        subscription.Should().NotBeSameAs(eventSubscription);

        await _stripeFacade.Received().GetSubscription(
            apiSubscription.Id,
            Arg.Is<SubscriptionGetOptions>(options => options.Expand == expand),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    #region ValidateCloudRegion
    [Fact]
    public async Task ValidateCloudRegion_SubscriptionUpdated_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        var subscription = Copy(stripeEvent.Data.Object as Subscription);

        var customer = await GetCustomerAsync();

        subscription.Customer = customer;

        _stripeFacade.GetSubscription(
                subscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetSubscription(
            subscription.Id,
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_ChargeSucceeded_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.ChargeSucceeded);

        var charge = Copy(stripeEvent.Data.Object as Charge);

        var customer = await GetCustomerAsync();

        charge.Customer = customer;

        _stripeFacade.GetCharge(
                charge.Id,
                Arg.Any<ChargeGetOptions>())
            .Returns(charge);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetCharge(
            charge.Id,
            Arg.Any<ChargeGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_UpcomingInvoice_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceUpcoming);

        var invoice = Copy(stripeEvent.Data.Object as Invoice);

        var customer = await GetCustomerAsync();

        _stripeFacade.GetCustomer(
                invoice.CustomerId,
                Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetCustomer(
            invoice.CustomerId,
            Arg.Any<CustomerGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_InvoiceCreated_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.InvoiceCreated);

        var invoice = Copy(stripeEvent.Data.Object as Invoice);

        var customer = await GetCustomerAsync();

        invoice.Customer = customer;

        _stripeFacade.GetInvoice(
                invoice.Id,
                Arg.Any<InvoiceGetOptions>())
            .Returns(invoice);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetInvoice(
            invoice.Id,
            Arg.Any<InvoiceGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_PaymentMethodAttached_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.PaymentMethodAttached);

        var paymentMethod = Copy(stripeEvent.Data.Object as PaymentMethod);

        var customer = await GetCustomerAsync();

        paymentMethod.Customer = customer;

        _stripeFacade.GetPaymentMethod(
                paymentMethod.Id,
                Arg.Any<PaymentMethodGetOptions>())
            .Returns(paymentMethod);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetPaymentMethod(
            paymentMethod.Id,
            Arg.Any<PaymentMethodGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_CustomerUpdated_Success()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated);

        var customer = Copy(stripeEvent.Data.Object as Customer);

        _stripeFacade.GetCustomer(
                customer.Id,
                Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetCustomer(
            customer.Id,
            Arg.Any<CustomerGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataNull_ReturnsFalse()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        var subscription = Copy(stripeEvent.Data.Object as Subscription);

        var customer = await GetCustomerAsync();
        customer.Metadata = null;

        subscription.Customer = customer;

        _stripeFacade.GetSubscription(
                subscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeFalse();

        await _stripeFacade.Received(1).GetSubscription(
            subscription.Id,
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataNoRegion_DefaultUS_ReturnsTrue()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        var subscription = Copy(stripeEvent.Data.Object as Subscription);

        var customer = await GetCustomerAsync();
        customer.Metadata = new Dictionary<string, string>();

        subscription.Customer = customer;

        _stripeFacade.GetSubscription(
                subscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetSubscription(
            subscription.Id,
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataMiscasedRegion_ReturnsTrue()
    {
        // Arrange
        var stripeEvent = await StripeTestEvents.GetAsync(StripeEventType.CustomerSubscriptionUpdated);

        var subscription = Copy(stripeEvent.Data.Object as Subscription);

        var customer = await GetCustomerAsync();
        customer.Metadata = new Dictionary<string, string>
        {
            { "Region", "US" }
        };

        subscription.Customer = customer;

        _stripeFacade.GetSubscription(
                subscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        cloudRegionValid.Should().BeTrue();

        await _stripeFacade.Received(1).GetSubscription(
            subscription.Id,
            Arg.Any<SubscriptionGetOptions>(),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }
    #endregion

    private static T Copy<T>(T input)
    {
        var copy = (T)Activator.CreateInstance(typeof(T));

        var properties = input.GetType().GetProperties();

        foreach (var property in properties)
        {
            var value = property.GetValue(input);
            copy!
                .GetType()
                .GetProperty(property.Name)!
                .SetValue(copy, value);
        }

        return copy;
    }

    private static async Task<Customer> GetCustomerAsync()
        => (await StripeTestEvents.GetAsync(StripeEventType.CustomerUpdated)).Data.Object as Customer;
}
