using Bit.Core.Billing;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
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
    private readonly IGlobalSettings _globalSettings = Substitute.For<IGlobalSettings>();
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IHasPaymentMethodQuery _hasPaymentMethodQuery = Substitute.For<IHasPaymentMethodQuery>();
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

        _command = new CreatePremiumCloudHostedSubscriptionCommand(
            _braintreeGateway,
            _globalSettings,
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService,
            _userService,
            _pushNotificationService,
            Substitute.For<ILogger<CreatePremiumCloudHostedSubscriptionCommand>>(),
            _pricingClient,
            _hasPaymentMethodQuery,
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _stripeAdapter.SetupIntentList(Arg.Any<SetupIntentListOptions>()).Returns(Task.FromResult(new List<SetupIntent> { mockSetupIntent }));
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
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
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active";

        var mockInvoice = Substitute.For<Invoice>();

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
        await _subscriberService.Received(1).CreateBraintreeCustomer(user, paymentMethod.Token);
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
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

        // Mock that the user has a payment method (this is the key difference from the credit purchase case)
        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(true);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        await _stripeAdapter.DidNotReceive().CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        await _updatePaymentMethodCommand.DidNotReceive().Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>());
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

        // Mock that the user does NOT have a payment method (simulating credit purchase scenario)
        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(false);
        _updatePaymentMethodCommand.Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>())
            .Returns(mockMaskedPaymentMethod);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        // Verify that update payment method was called (new behavior for credit purchase case)
        await _updatePaymentMethodCommand.Received(1).Run(user, paymentMethod, billingAddress);
        // Verify GetCustomerOrThrow was called after updating payment method
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        // Verify no new customer was created
        await _stripeAdapter.DidNotReceive().CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        // Verify subscription was created
        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        Assert.Equal(mockSubscription.GetCurrentPeriodEnd(), user.PremiumExpirationDate);
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
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
        mockCustomer.Metadata = new Dictionary<string, string>();

        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = "sub_123";
        mockSubscription.Status = "active"; // PayPal + active doesn't match pattern
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        Assert.False(user.Premium);
        Assert.Null(user.PremiumExpirationDate);
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        _stripeAdapter.SetupIntentList(Arg.Any<SetupIntentListOptions>())
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
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.InvoiceUpdateAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, 0);

        // Assert
        Assert.True(result.IsT0);
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        await _stripeAdapter.DidNotReceive().CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
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
        await _stripeAdapter.DidNotReceive().CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.DidNotReceive().SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
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

        _stripeAdapter.CustomerCreateAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);

        // Act
        var result = await _command.Run(user, paymentMethod, billingAddress, additionalStorage);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal((short)3, user.MaxStorageGb); // 1 (provided) + 2 (additional) = 3
        await _userService.Received(1).SaveUserAsync(user);
    }

}
