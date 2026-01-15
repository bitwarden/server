using Bit.Core.Billing;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using Address = Stripe.Address;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;
using StripeCustomer = Stripe.Customer;
using StripeSubscription = Stripe.Subscription;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class CreatePremiumCloudHostedSubscriptionCommandTests
{
    private readonly IBraintreeGateway _braintreeGateway = Substitute.For<IBraintreeGateway>();
    private readonly IBraintreeService _braintreeService = Substitute.For<IBraintreeService>();
    private readonly IGlobalSettings _globalSettings = Substitute.For<IGlobalSettings>();
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUpdatePaymentMethodCommand _updatePaymentMethodCommand = Substitute.For<IUpdatePaymentMethodCommand>();
    private readonly CreatePremiumCloudHostedSubscriptionCommand _command;

    public CreatePremiumCloudHostedSubscriptionCommandTests()
    {
        var baseServiceUri = Substitute.For<IBaseServiceUriSettings>();
        baseServiceUri.CloudRegion.Returns("US");
        _globalSettings.BaseServiceUri.Returns(baseServiceUri);

        // Setup default premium plan with standard pricing
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = StripeConstants.Prices.PremiumAnnually },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = StripeConstants.Prices.StoragePlanPersonal, Provided = 1 }
        };
        _pricingClient.GetAvailablePremiumPlan().Returns(premiumPlan);

        // Mock ListSubscriptionsAsync to return empty list by default (no existing subscriptions)
        // Individual tests can override this if they need to test duplicate subscription scenarios
        _stripeAdapter.ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>()).Returns(new List<Stripe.Subscription>());

        // Mock GetCustomerAsync to return a customer with email when needed for email-based duplicate checks
        // Individual tests can override this if they need specific customer behavior
        _stripeAdapter.GetCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerGetOptions>())
            .Returns(callInfo =>
            {
                var customerId = callInfo.Arg<string>();
                var customer = Substitute.For<StripeCustomer>();
                customer.Id = customerId;
                customer.Email = "test@example.com"; // Default email for tests
                return Task.FromResult(customer);
            });

        // Mock CancelSubscriptionAsync to avoid issues if duplicate cleanup logic is triggered
        _stripeAdapter.CancelSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>()).Returns(Task.FromResult(new Stripe.Subscription()));

        _command = new CreatePremiumCloudHostedSubscriptionCommand(
            _braintreeGateway,
            _braintreeService,
            _globalSettings,
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService,
            _userService,
            _pushNotificationService,
            Substitute.For<ILogger<CreatePremiumCloudHostedSubscriptionCommand>>(),
            _pricingClient,
            _updatePaymentMethodCommand);
    }

    [Theory, BitAutoData]
    public async Task Run_UserAlreadyPremium_ReturnsBadRequest(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Already a premium user.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_NegativeStorageAmount_ReturnsBadRequest(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, -1);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Additional storage must be greater than 0.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidPaymentMethodTypes_BankAccount_Success(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null; // Ensure no existing customer ID
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.BankAccount;
        paymentMethod.Token = "bank_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        var mockSetupIntent = Substitute.For<SetupIntent>();
        mockSetupIntent.Id = "seti_123";

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _stripeAdapter.ListSetupIntentsAsync(Arg.Any<SetupIntentListOptions>()).Returns(Task.FromResult(new List<SetupIntent> { mockSetupIntent }));
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidPaymentMethodTypes_Card_Success(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidPaymentMethodTypes_PayPal_Success(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.PayPal;
        paymentMethod.Token = "paypal_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>
        {
            [Core.Billing.Utilities.BraintreeCustomerIdKey] = "bt_customer_123"
        };

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.LatestInvoiceId = "in_123";

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        await _subscriberService.Received(1).CreateBraintreeCustomer(user, paymentMethod.Token);
        await _stripeAdapter.Received(1).UpdateInvoiceAsync(mockSubscription.LatestInvoiceId,
            Arg.Is<InvoiceUpdateOptions>(opts =>
                opts.AutoAdvance == false &&
                opts.Expand != null &&
                opts.Expand.Contains("customer")));
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), mockInvoice);
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidRequestWithAdditionalStorage_Success(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";
        const short additionalStorage = 2;

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, additionalStorage);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        Assert.Equal((short)(1 + additionalStorage), user.MaxStorageGb);
        Assert.NotNull(user.LicenseKey);
        Assert.Equal(20, user.LicenseKey.Length);
        Assert.NotEqual(default, user.RevisionDate);
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_UserHasExistingGatewayCustomerIdAndPaymentMethod_UsesExistingCustomer(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_customer_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();
        MaskedPaymentMethod mockMaskedPaymentMethod = new MaskedCard
        {
            Brand = "visa",
            Last4 = "1234",
            Expiration = "12/2025"
        };

        _updatePaymentMethodCommand.Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>())
            .Returns(mockMaskedPaymentMethod);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _updatePaymentMethodCommand.Received(1).Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>());
    }

    [Theory, BitAutoData]
    public async Task Run_UserPreviouslyPurchasedCreditWithoutPaymentMethod_UpdatesPaymentMethodAndCreatesSubscription(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_customer_123"; // Customer exists from previous credit purchase
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();
        MaskedPaymentMethod mockMaskedPaymentMethod = new MaskedCard
        {
            Brand = "visa",
            Last4 = "1234",
            Expiration = "12/2025"
        };

        _updatePaymentMethodCommand.Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>())
            .Returns(mockMaskedPaymentMethod);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        // Verify that update payment method was called (new behavior for credit purchase case)
        await _updatePaymentMethodCommand.Received(1).Run(user, paymentMethod, billingAddress);
        // Verify GetCustomerOrThrow was called after updating payment method
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        // Verify no new customer was created
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        // Verify subscription was created
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        // Verify user was updated correctly
        Assert.True(user.Premium);
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_PayPalWithIncompleteSubscription_SetsPremiumTrue(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        user.PremiumExpirationDate = null;
        paymentMethod.Type = TokenizablePaymentMethodType.PayPal;
        paymentMethod.Token = "paypal_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>
        {
            [Core.Billing.Utilities.BraintreeCustomerIdKey] = "bt_customer_123"
        };

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "incomplete";
        mockSubscription.LatestInvoiceId = "in_123";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        Assert.Equal(mockSubscription.GetCurrentPeriodEnd(), user.PremiumExpirationDate);
        await _stripeAdapter.Received(1).UpdateInvoiceAsync(mockSubscription.LatestInvoiceId,
            Arg.Is<InvoiceUpdateOptions>(opts =>
                opts.AutoAdvance == false &&
                opts.Expand != null &&
                opts.Expand.Contains("customer")));
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), mockInvoice);
    }

    [Theory, BitAutoData]
    public async Task Run_NonPayPalWithActiveSubscription_SetsPremiumTrue(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        Assert.Equal(mockSubscription.GetCurrentPeriodEnd(), user.PremiumExpirationDate);
    }

    [Theory, BitAutoData]
    public async Task Run_SubscriptionStatusDoesNotMatchPatterns_DoesNotSetPremium(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        user.PremiumExpirationDate = null;
        paymentMethod.Type = TokenizablePaymentMethodType.PayPal;
        paymentMethod.Token = "paypal_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>
        {
            [Core.Billing.Utilities.BraintreeCustomerIdKey] = "bt_customer_123"
        };

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active"; // PayPal + active doesn't match pattern
        mockSubscription.LatestInvoiceId = "in_123";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.False(user.Premium);
        Assert.Null(user.PremiumExpirationDate);
        await _stripeAdapter.Received(1).UpdateInvoiceAsync(mockSubscription.LatestInvoiceId,
            Arg.Is<InvoiceUpdateOptions>(opts =>
                opts.AutoAdvance == false &&
                opts.Expand != null &&
                opts.Expand.Contains("customer")));
        await _braintreeService.Received(1).PayInvoice(Arg.Any<SubscriberId>(), mockInvoice);
    }

    [Theory, BitAutoData]
    public async Task Run_BankAccountWithNoSetupIntentFound_ReturnsUnhandled(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.BankAccount;
        paymentMethod.Token = "bank_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "incomplete";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        _stripeAdapter.ListSetupIntentsAsync(Arg.Any<SetupIntentListOptions>())
            .Returns(Task.FromResult(new List<SetupIntent>())); // Empty list - no setup intent found

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.Equal("Something went wrong with your request. Please contact support for assistance.", unhandled.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_AccountCredit_WithExistingCustomer_Success(
        User user,
        NonTokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_customer_123";
        paymentMethod.Type = NonTokenizablePaymentMethodType.AccountCredit;
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        Assert.True(user.Premium);
        Assert.Equal(mockSubscription.GetCurrentPeriodEnd(), user.PremiumExpirationDate);
    }

    [Theory, BitAutoData]
    public async Task Run_NonTokenizedPaymentWithoutExistingCustomer_ThrowsBillingException(
        User user,
        NonTokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        // No existing gateway customer ID
        user.GatewayCustomerId = null;
        paymentMethod.Type = NonTokenizablePaymentMethodType.AccountCredit;
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        //Assert
        Assert.True(result.IsT3); // Assuming T3 is the Unhandled result
        Assert.IsType<BillingException>(result.AsT3.Exception);
        // Verify no customer was created or subscription attempted
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        await _userService.DidNotReceive().SaveUserAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task Run_WithAdditionalStorage_SetsCorrectMaxStorageGb(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";
        const short additionalStorage = 2;

        // Setup premium plan with 5GB provided storage
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = StripeConstants.Prices.PremiumAnnually },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = StripeConstants.Prices.StoragePlanPersonal, Provided = 1 }
        };
        _pricingClient.GetAvailablePremiumPlan().Returns(premiumPlan);

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, additionalStorage);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal((short)3, user.MaxStorageGb); // 1 (provided) + 2 (additional) = 3
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithoutGatewayCustomerId_ExistingSubscriptionByEmail_ReturnsBadRequest(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        // Setup an existing Premium subscription for a customer with the same email
        var existingCustomer = Substitute.For<StripeCustomer>();
        existingCustomer.Id = "existing_cust_123";
        existingCustomer.Email = "test@example.com";

        var existingSubscription = Substitute.For<StripeSubscription>();
        existingSubscription.Id = "existing_sub_123";
        existingSubscription.Status = "active";
        // Set Customer as the Customer object (expanded) so we can check email directly
        existingSubscription.Customer = existingCustomer;
        existingSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    Price = new Price { Id = StripeConstants.Prices.PremiumAnnually }
                }
            ]
        };

        // Mock ListSubscriptionsAsync to return the existing subscription
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Status == "active" && opts.Customer == null))
            .Returns(new List<StripeSubscription> { existingSubscription });

        // Mock GetCustomerAsync to return the customer with matching email
        _stripeAdapter.GetCustomerAsync(existingCustomer.Id, Arg.Any<CustomerGetOptions>())
            .Returns(existingCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("already have an active Premium subscription", badRequest.Response);
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithGatewayCustomerId_ExistingSubscription_ReturnsBadRequest(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_cust_123";
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var existingSubscription = Substitute.For<StripeSubscription>();
        existingSubscription.Id = "existing_sub_123";
        existingSubscription.Status = "active";
        existingSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    Price = new Price { Id = StripeConstants.Prices.PremiumAnnually }
                }
            ]
        };

        // Mock ListSubscriptionsAsync to return the existing subscription for the customer
        _stripeAdapter.ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Status == "active" && opts.Customer == user.GatewayCustomerId))
            .Returns(new List<StripeSubscription> { existingSubscription });

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("already have an active Premium subscription", badRequest.Response);
        await _stripeAdapter.DidNotReceive().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithoutGatewayCustomerId_NoExistingSubscription_Success(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "cust_123";
        mockCustomer.Email = "test@example.com";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";
        mockSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        var mockInvoice = Substitute.For<Invoice>();

        // Mock ListSubscriptionsAsync to return empty list (no existing subscriptions)
        // This will be called twice: once for email check, once for customer ID check
        _stripeAdapter.ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>()).Returns(new List<StripeSubscription>());

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>(), Arg.Any<RequestOptions>());
        await _userService.Received(1).SaveUserAsync(user);
    }

}
