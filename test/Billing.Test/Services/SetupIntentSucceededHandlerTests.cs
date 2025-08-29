using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Stripe;
using Xunit;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class SetupIntentSucceededHandlerTests
{
    private static readonly Event _mockEvent = new() { Id = "evt_test", Type = "setup_intent.succeeded" };
    private static readonly string[] _expand = ["payment_method"];

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly ISetupIntentCache _setupIntentCache;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IStripeEventService _stripeEventService;
    private readonly SetupIntentSucceededHandler _handler;

    public SetupIntentSucceededHandlerTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
        _setupIntentCache = Substitute.For<ISetupIntentCache>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _stripeEventService = Substitute.For<IStripeEventService>();

        _handler = new SetupIntentSucceededHandler(
            _organizationRepository,
            _providerRepository,
            _pushNotificationAdapter,
            _setupIntentCache,
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
        await _setupIntentCache.DidNotReceiveWithAnyArgs().GetSubscriberIdForSetupIntent(Arg.Any<string>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().PaymentMethodAttachAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_NoSubscriberIdInCache_Returns()
    {
        // Arrange
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns((Guid?)null);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().PaymentMethodAttachAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_ValidOrganization_AttachesPaymentMethodAndSendsNotification()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = new Organization { Id = organizationId, Name = "Test Org", GatewayCustomerId = "cus_test" };
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns(organizationId);

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.Received(1).PaymentMethodAttachAsync(
            "pm_test",
            Arg.Is<PaymentMethodAttachOptions>(o => o.Customer == organization.GatewayCustomerId));

        await _pushNotificationAdapter.Received(1).NotifyBankAccountVerifiedAsync(organization);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_ValidProvider_AttachesPaymentMethodAndSendsNotification()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new Provider { Id = providerId, Name = "Test Provider", GatewayCustomerId = "cus_test" };
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns(providerId);

        _organizationRepository.GetByIdAsync(providerId)
            .Returns((Organization?)null);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.Received(1).PaymentMethodAttachAsync(
            "pm_test",
            Arg.Is<PaymentMethodAttachOptions>(o => o.Customer == provider.GatewayCustomerId));

        await _pushNotificationAdapter.Received(1).NotifyBankAccountVerifiedAsync(provider);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_OrganizationWithoutGatewayCustomerId_DoesNotAttachPaymentMethod()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = new Organization { Id = organizationId, Name = "Test Org", GatewayCustomerId = null };
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns(organizationId);

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().PaymentMethodAttachAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_ProviderWithoutGatewayCustomerId_DoesNotAttachPaymentMethod()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new Provider { Id = providerId, Name = "Test Provider", GatewayCustomerId = null };
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns(providerId);

        _organizationRepository.GetByIdAsync(providerId)
            .Returns((Organization?)null);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().PaymentMethodAttachAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_OrganizationNotFound_DoesNotSendNotification()
    {
        // Arrange
        var subscriberId = Guid.NewGuid();
        var setupIntent = CreateSetupIntent();

        _stripeEventService.GetSetupIntent(
                _mockEvent,
                true,
                Arg.Is<List<string>>(options => options.SequenceEqual(_expand)))
            .Returns(setupIntent);

        _setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id)
            .Returns(subscriberId);

        _organizationRepository.GetByIdAsync(subscriberId)
            .Returns((Organization?)null);

        _providerRepository.GetByIdAsync(subscriberId)
            .Returns((Provider?)null);

        // Act
        await _handler.HandleAsync(_mockEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().PaymentMethodAttachAsync(
            Arg.Any<string>(), Arg.Any<PaymentMethodAttachOptions>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Organization>());
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyBankAccountVerifiedAsync(Arg.Any<Provider>());
    }

    private static SetupIntent CreateSetupIntent(bool hasUSBankAccount = true)
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
            PaymentMethod = paymentMethod
        };

        return setupIntent;
    }
}
