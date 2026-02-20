using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class SetupIntentSucceededHandlerTests
{
    private static readonly Event _mockEvent = new() { Id = "evt_test", Type = "setup_intent.succeeded" };
    private static readonly string[] _expand = ["payment_method"];

    private readonly ILogger<SetupIntentSucceededHandler> _logger;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IStripeEventService _stripeEventService;
    private readonly SetupIntentSucceededHandler _handler;

    public SetupIntentSucceededHandlerTests()
    {
        _logger = Substitute.For<ILogger<SetupIntentSucceededHandler>>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _stripeEventService = Substitute.For<IStripeEventService>();

        _handler = new SetupIntentSucceededHandler(
            _logger,
            _organizationRepository,
            _providerRepository,
            _pushNotificationAdapter,
            _stripeAdapter,
            _stripeEventService);
    }

    [Fact]
    public async Task HandleAsync_PaymentMethodNotUSBankAccount_Returns()
    {
        // Arrange
        var setupIntent = CreateSetupIntent(hasUSBankAccount: false);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByGatewayCustomerIdAsync(Arg.Any<string>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().AttachPaymentMethodAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_NoCustomerIdOnSetupIntent_Returns()
    {
        // Arrange
        var setupIntent = CreateSetupIntent(customerId: null);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByGatewayCustomerIdAsync(Arg.Any<string>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().AttachPaymentMethodAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_NoOrganizationOrProviderFound_LogsErrorAndReturns()
    {
        // Arrange
        var customerId = "cus_test";
        var setupIntent = CreateSetupIntent(customerId: customerId);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _organizationRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns((Organization?)null);

        _providerRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns((Provider?)null);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().AttachPaymentMethodAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_ValidOrganization_AttachesPaymentMethodAndSendsNotification()
    {
        // Arrange
        var customerId = "cus_test";
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Test Org", GatewayCustomerId = customerId };
        var setupIntent = CreateSetupIntent(customerId: customerId);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _organizationRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns(organization);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.Received(1).AttachPaymentMethodAsync(
            "pm_test",
            Arg.Is<PaymentMethodAttachOptions>(o => o.Customer == organization.GatewayCustomerId));

        await _pushNotificationAdapter.Received(1).NotifyBankAccountVerifiedAsync(organization);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());

        // Provider should not be queried when organization is found
        await _providerRepository.DidNotReceiveWithAnyArgs().GetByGatewayCustomerIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_ValidProvider_AttachesPaymentMethodAndSendsNotification()
    {
        // Arrange
        var customerId = "cus_test";
        var provider = new Provider { Id = Guid.NewGuid(), Name = "Test Provider", GatewayCustomerId = customerId };
        var setupIntent = CreateSetupIntent(customerId: customerId);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _organizationRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns((Organization?)null);

        _providerRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns(provider);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.Received(1).AttachPaymentMethodAsync(
            "pm_test",
            Arg.Is<PaymentMethodAttachOptions>(o => o.Customer == provider.GatewayCustomerId));

        await _pushNotificationAdapter.Received(1).NotifyBankAccountVerifiedAsync(provider);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_OrganizationWithoutGatewayCustomerId_DoesNotAttachPaymentMethod()
    {
        // Arrange
        var customerId = "cus_test";
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Test Org", GatewayCustomerId = null };
        var setupIntent = CreateSetupIntent(customerId: customerId);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _organizationRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns(organization);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().AttachPaymentMethodAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_ProviderWithoutGatewayCustomerId_DoesNotAttachPaymentMethod()
    {
        // Arrange
        var customerId = "cus_test";
        var provider = new Provider { Id = Guid.NewGuid(), Name = "Test Provider", GatewayCustomerId = null };
        var setupIntent = CreateSetupIntent(customerId: customerId);

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _organizationRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns((Organization?)null);

        _providerRepository.GetByGatewayCustomerIdAsync(customerId)
            .Returns(provider);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().AttachPaymentMethodAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    private static SetupIntent CreateSetupIntent(bool hasUSBankAccount = true, string? customerId = "cus_default")
    {
        var paymentMethod = new PaymentMethod
        {
            Id = "pm_test",
            Type = "us_bank_account",
            UsBankAccount = hasUSBankAccount ? new PaymentMethodUsBankAccount() : null
        };

        var setupIntent = new SetupIntent
        {
            Id = "seti_test",
            CustomerId = customerId,
            PaymentMethod = paymentMethod
        };

        return setupIntent;
    }
}
