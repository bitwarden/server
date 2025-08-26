using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.Settings;
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
        _stripeEventService = new StripeEventService(globalSettings, _stripeFacade);
    }

    #region GetCharge
    [Fact]
    public async Task GetCharge_EventNotChargeRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "invoice.created", new Invoice { Id = "in_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetCharge(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Charge)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCharge(
            Arg.Any<string>(),
            Arg.Any<ChargeGetOptions>());
    }

    [Fact]
    public async Task GetCharge_NotFresh_ReturnsEventCharge()
    {
        // Arrange
        var mockCharge = new Charge { Id = "ch_test", Amount = 1000 };
        var stripeEvent = CreateMockEvent("evt_test", "charge.succeeded", mockCharge);

        // Act
        var charge = await _stripeEventService.GetCharge(stripeEvent);

        // Assert
        Assert.Equal(mockCharge.Id, charge.Id);
        Assert.Equal(mockCharge.Amount, charge.Amount);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCharge(
            Arg.Any<string>(),
            Arg.Any<ChargeGetOptions>());
    }

    [Fact]
    public async Task GetCharge_Fresh_Expand_ReturnsAPICharge()
    {
        // Arrange
        var eventCharge = new Charge { Id = "ch_test", Amount = 1000 };
        var stripeEvent = CreateMockEvent("evt_test", "charge.succeeded", eventCharge);

        var apiCharge = new Charge { Id = "ch_test", Amount = 2000 };

        var expand = new List<string> { "customer" };

        _stripeFacade.GetCharge(
                apiCharge.Id,
                Arg.Is<ChargeGetOptions>(options => options.Expand == expand))
            .Returns(apiCharge);

        // Act
        var charge = await _stripeEventService.GetCharge(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiCharge, charge);
        Assert.NotSame(eventCharge, charge);

        await _stripeFacade.Received().GetCharge(
            apiCharge.Id,
            Arg.Is<ChargeGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region GetCustomer
    [Fact]
    public async Task GetCustomer_EventNotCustomerRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "invoice.created", new Invoice { Id = "in_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetCustomer(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Customer)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCustomer(
            Arg.Any<string>(),
            Arg.Any<CustomerGetOptions>());
    }

    [Fact]
    public async Task GetCustomer_NotFresh_ReturnsEventCustomer()
    {
        // Arrange
        var mockCustomer = new Customer { Id = "cus_test", Email = "test@example.com" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", mockCustomer);

        // Act
        var customer = await _stripeEventService.GetCustomer(stripeEvent);

        // Assert
        Assert.Equal(mockCustomer.Id, customer.Id);
        Assert.Equal(mockCustomer.Email, customer.Email);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetCustomer(
            Arg.Any<string>(),
            Arg.Any<CustomerGetOptions>());
    }

    [Fact]
    public async Task GetCustomer_Fresh_Expand_ReturnsAPICustomer()
    {
        // Arrange
        var eventCustomer = new Customer { Id = "cus_test", Email = "test@example.com" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", eventCustomer);

        var apiCustomer = new Customer { Id = "cus_test", Email = "updated@example.com" };

        var expand = new List<string> { "subscriptions" };

        _stripeFacade.GetCustomer(
                apiCustomer.Id,
                Arg.Is<CustomerGetOptions>(options => options.Expand == expand))
            .Returns(apiCustomer);

        // Act
        var customer = await _stripeEventService.GetCustomer(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiCustomer, customer);
        Assert.NotSame(eventCustomer, customer);

        await _stripeFacade.Received().GetCustomer(
            apiCustomer.Id,
            Arg.Is<CustomerGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region GetInvoice
    [Fact]
    public async Task GetInvoice_EventNotInvoiceRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", new Customer { Id = "cus_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetInvoice(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Invoice)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetInvoice(
            Arg.Any<string>(),
            Arg.Any<InvoiceGetOptions>());
    }

    [Fact]
    public async Task GetInvoice_NotFresh_ReturnsEventInvoice()
    {
        // Arrange
        var mockInvoice = new Invoice { Id = "in_test", AmountDue = 1000 };
        var stripeEvent = CreateMockEvent("evt_test", "invoice.created", mockInvoice);

        // Act
        var invoice = await _stripeEventService.GetInvoice(stripeEvent);

        // Assert
        Assert.Equal(mockInvoice.Id, invoice.Id);
        Assert.Equal(mockInvoice.AmountDue, invoice.AmountDue);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetInvoice(
            Arg.Any<string>(),
            Arg.Any<InvoiceGetOptions>());
    }

    [Fact]
    public async Task GetInvoice_Fresh_Expand_ReturnsAPIInvoice()
    {
        // Arrange
        var eventInvoice = new Invoice { Id = "in_test", AmountDue = 1000 };
        var stripeEvent = CreateMockEvent("evt_test", "invoice.created", eventInvoice);

        var apiInvoice = new Invoice { Id = "in_test", AmountDue = 2000 };

        var expand = new List<string> { "customer" };

        _stripeFacade.GetInvoice(
                apiInvoice.Id,
                Arg.Is<InvoiceGetOptions>(options => options.Expand == expand))
            .Returns(apiInvoice);

        // Act
        var invoice = await _stripeEventService.GetInvoice(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiInvoice, invoice);
        Assert.NotSame(eventInvoice, invoice);

        await _stripeFacade.Received().GetInvoice(
            apiInvoice.Id,
            Arg.Is<InvoiceGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region GetPaymentMethod
    [Fact]
    public async Task GetPaymentMethod_EventNotPaymentMethodRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", new Customer { Id = "cus_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetPaymentMethod(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(PaymentMethod)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentMethod(
            Arg.Any<string>(),
            Arg.Any<PaymentMethodGetOptions>());
    }

    [Fact]
    public async Task GetPaymentMethod_NotFresh_ReturnsEventPaymentMethod()
    {
        // Arrange
        var mockPaymentMethod = new PaymentMethod { Id = "pm_test", Type = "card" };
        var stripeEvent = CreateMockEvent("evt_test", "payment_method.attached", mockPaymentMethod);

        // Act
        var paymentMethod = await _stripeEventService.GetPaymentMethod(stripeEvent);

        // Assert
        Assert.Equal(mockPaymentMethod.Id, paymentMethod.Id);
        Assert.Equal(mockPaymentMethod.Type, paymentMethod.Type);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentMethod(
            Arg.Any<string>(),
            Arg.Any<PaymentMethodGetOptions>());
    }

    [Fact]
    public async Task GetPaymentMethod_Fresh_Expand_ReturnsAPIPaymentMethod()
    {
        // Arrange
        var eventPaymentMethod = new PaymentMethod { Id = "pm_test", Type = "card" };
        var stripeEvent = CreateMockEvent("evt_test", "payment_method.attached", eventPaymentMethod);

        var apiPaymentMethod = new PaymentMethod { Id = "pm_test", Type = "card" };

        var expand = new List<string> { "customer" };

        _stripeFacade.GetPaymentMethod(
                apiPaymentMethod.Id,
                Arg.Is<PaymentMethodGetOptions>(options => options.Expand == expand))
            .Returns(apiPaymentMethod);

        // Act
        var paymentMethod = await _stripeEventService.GetPaymentMethod(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiPaymentMethod, paymentMethod);
        Assert.NotSame(eventPaymentMethod, paymentMethod);

        await _stripeFacade.Received().GetPaymentMethod(
            apiPaymentMethod.Id,
            Arg.Is<PaymentMethodGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region GetSubscription
    [Fact]
    public async Task GetSubscription_EventNotSubscriptionRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", new Customer { Id = "cus_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetSubscription(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(Subscription)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(
            Arg.Any<string>(),
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task GetSubscription_NotFresh_ReturnsEventSubscription()
    {
        // Arrange
        var mockSubscription = new Subscription { Id = "sub_test", Status = "active" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", mockSubscription);

        // Act
        var subscription = await _stripeEventService.GetSubscription(stripeEvent);

        // Assert
        Assert.Equal(mockSubscription.Id, subscription.Id);
        Assert.Equal(mockSubscription.Status, subscription.Status);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(
            Arg.Any<string>(),
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task GetSubscription_Fresh_Expand_ReturnsAPISubscription()
    {
        // Arrange
        var eventSubscription = new Subscription { Id = "sub_test", Status = "active" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", eventSubscription);

        var apiSubscription = new Subscription { Id = "sub_test", Status = "canceled" };

        var expand = new List<string> { "customer" };

        _stripeFacade.GetSubscription(
                apiSubscription.Id,
                Arg.Is<SubscriptionGetOptions>(options => options.Expand == expand))
            .Returns(apiSubscription);

        // Act
        var subscription = await _stripeEventService.GetSubscription(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiSubscription, subscription);
        Assert.NotSame(eventSubscription, subscription);

        await _stripeFacade.Received().GetSubscription(
            apiSubscription.Id,
            Arg.Is<SubscriptionGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region GetSetupIntent
    [Fact]
    public async Task GetSetupIntent_EventNotSetupIntentRelated_ThrowsException()
    {
        // Arrange
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", new Customer { Id = "cus_test" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => await _stripeEventService.GetSetupIntent(stripeEvent));
        Assert.Equal($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{nameof(SetupIntent)}'", exception.Message);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSetupIntent(
            Arg.Any<string>(),
            Arg.Any<SetupIntentGetOptions>());
    }

    [Fact]
    public async Task GetSetupIntent_NotFresh_ReturnsEventSetupIntent()
    {
        // Arrange
        var mockSetupIntent = new SetupIntent { Id = "seti_test", Status = "succeeded" };
        var stripeEvent = CreateMockEvent("evt_test", "setup_intent.succeeded", mockSetupIntent);

        // Act
        var setupIntent = await _stripeEventService.GetSetupIntent(stripeEvent);

        // Assert
        Assert.Equal(mockSetupIntent.Id, setupIntent.Id);
        Assert.Equal(mockSetupIntent.Status, setupIntent.Status);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSetupIntent(
            Arg.Any<string>(),
            Arg.Any<SetupIntentGetOptions>());
    }

    [Fact]
    public async Task GetSetupIntent_Fresh_Expand_ReturnsAPISetupIntent()
    {
        // Arrange
        var eventSetupIntent = new SetupIntent { Id = "seti_test", Status = "succeeded" };
        var stripeEvent = CreateMockEvent("evt_test", "setup_intent.succeeded", eventSetupIntent);

        var apiSetupIntent = new SetupIntent { Id = "seti_test", Status = "requires_action" };

        var expand = new List<string> { "customer" };

        _stripeFacade.GetSetupIntent(
                apiSetupIntent.Id,
                Arg.Is<SetupIntentGetOptions>(options => options.Expand == expand))
            .Returns(apiSetupIntent);

        // Act
        var setupIntent = await _stripeEventService.GetSetupIntent(stripeEvent, true, expand);

        // Assert
        Assert.Equal(apiSetupIntent, setupIntent);
        Assert.NotSame(eventSetupIntent, setupIntent);

        await _stripeFacade.Received().GetSetupIntent(
            apiSetupIntent.Id,
            Arg.Is<SetupIntentGetOptions>(options => options.Expand == expand));
    }
    #endregion

    #region ValidateCloudRegion
    [Fact]
    public async Task ValidateCloudRegion_SubscriptionUpdated_Success()
    {
        // Arrange
        var mockSubscription = new Subscription { Id = "sub_test" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", mockSubscription);

        var customer = CreateMockCustomer();
        mockSubscription.Customer = customer;

        _stripeFacade.GetSubscription(
                mockSubscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetSubscription(
            mockSubscription.Id,
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_ChargeSucceeded_Success()
    {
        // Arrange
        var mockCharge = new Charge { Id = "ch_test" };
        var stripeEvent = CreateMockEvent("evt_test", "charge.succeeded", mockCharge);

        var customer = CreateMockCustomer();
        mockCharge.Customer = customer;

        _stripeFacade.GetCharge(
                mockCharge.Id,
                Arg.Any<ChargeGetOptions>())
            .Returns(mockCharge);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetCharge(
            mockCharge.Id,
            Arg.Any<ChargeGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_UpcomingInvoice_Success()
    {
        // Arrange
        var mockInvoice = new Invoice { Id = "in_test", CustomerId = "cus_test" };
        var stripeEvent = CreateMockEvent("evt_test", "invoice.upcoming", mockInvoice);

        var customer = CreateMockCustomer();

        _stripeFacade.GetCustomer(
                mockInvoice.CustomerId,
                Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetCustomer(
            mockInvoice.CustomerId,
            Arg.Any<CustomerGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_InvoiceCreated_Success()
    {
        // Arrange
        var mockInvoice = new Invoice { Id = "in_test" };
        var stripeEvent = CreateMockEvent("evt_test", "invoice.created", mockInvoice);

        var customer = CreateMockCustomer();
        mockInvoice.Customer = customer;

        _stripeFacade.GetInvoice(
                mockInvoice.Id,
                Arg.Any<InvoiceGetOptions>())
            .Returns(mockInvoice);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetInvoice(
            mockInvoice.Id,
            Arg.Any<InvoiceGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_PaymentMethodAttached_Success()
    {
        // Arrange
        var mockPaymentMethod = new PaymentMethod { Id = "pm_test" };
        var stripeEvent = CreateMockEvent("evt_test", "payment_method.attached", mockPaymentMethod);

        var customer = CreateMockCustomer();
        mockPaymentMethod.Customer = customer;

        _stripeFacade.GetPaymentMethod(
                mockPaymentMethod.Id,
                Arg.Any<PaymentMethodGetOptions>())
            .Returns(mockPaymentMethod);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetPaymentMethod(
            mockPaymentMethod.Id,
            Arg.Any<PaymentMethodGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_CustomerUpdated_Success()
    {
        // Arrange
        var mockCustomer = CreateMockCustomer();
        var stripeEvent = CreateMockEvent("evt_test", "customer.updated", mockCustomer);

        _stripeFacade.GetCustomer(
                mockCustomer.Id,
                Arg.Any<CustomerGetOptions>())
            .Returns(mockCustomer);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetCustomer(
            mockCustomer.Id,
            Arg.Any<CustomerGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataNull_ReturnsFalse()
    {
        // Arrange
        var mockSubscription = new Subscription { Id = "sub_test" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", mockSubscription);

        var customer = new Customer { Id = "cus_test", Metadata = null };
        mockSubscription.Customer = customer;

        _stripeFacade.GetSubscription(
                mockSubscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.False(cloudRegionValid);

        await _stripeFacade.Received(1).GetSubscription(
            mockSubscription.Id,
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataNoRegion_DefaultUS_ReturnsTrue()
    {
        // Arrange
        var mockSubscription = new Subscription { Id = "sub_test" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", mockSubscription);

        var customer = new Customer { Id = "cus_test", Metadata = new Dictionary<string, string>() };
        mockSubscription.Customer = customer;

        _stripeFacade.GetSubscription(
                mockSubscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetSubscription(
            mockSubscription.Id,
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_MetadataIncorrectlyCasedRegion_ReturnsTrue()
    {
        // Arrange
        var mockSubscription = new Subscription { Id = "sub_test" };
        var stripeEvent = CreateMockEvent("evt_test", "customer.subscription.updated", mockSubscription);

        var customer = new Customer
        {
            Id = "cus_test",
            Metadata = new Dictionary<string, string> { { "Region", "US" } }
        };
        mockSubscription.Customer = customer;

        _stripeFacade.GetSubscription(
                mockSubscription.Id,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetSubscription(
            mockSubscription.Id,
            Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task ValidateCloudRegion_SetupIntentSucceeded_Success()
    {
        // Arrange
        var mockSetupIntent = new SetupIntent { Id = "seti_test" };
        var stripeEvent = CreateMockEvent("evt_test", "setup_intent.succeeded", mockSetupIntent);

        var customer = CreateMockCustomer();
        mockSetupIntent.Customer = customer;

        _stripeFacade.GetSetupIntent(
                mockSetupIntent.Id,
                Arg.Any<SetupIntentGetOptions>())
            .Returns(mockSetupIntent);

        // Act
        var cloudRegionValid = await _stripeEventService.ValidateCloudRegion(stripeEvent);

        // Assert
        Assert.True(cloudRegionValid);

        await _stripeFacade.Received(1).GetSetupIntent(
            mockSetupIntent.Id,
            Arg.Any<SetupIntentGetOptions>());
    }
    #endregion

    private static Event CreateMockEvent<T>(string id, string type, T dataObject) where T : IStripeEntity
    {
        return new Event
        {
            Id = id,
            Type = type,
            Data = new EventData
            {
                Object = (IHasObject)dataObject
            }
        };
    }

    private static Customer CreateMockCustomer()
    {
        return new Customer
        {
            Id = "cus_test",
            Metadata = new Dictionary<string, string> { { "region", "US" } }
        };
    }
}
