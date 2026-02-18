using Bit.Core.Billing;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Premium.Models;
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
    private readonly IHasPaymentMethodQuery _hasPaymentMethodQuery = Substitute.For<IHasPaymentMethodQuery>();
    private readonly IUpdatePaymentMethodCommand _updatePaymentMethodCommand = Substitute.For<IUpdatePaymentMethodCommand>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
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
            _braintreeService,
            _globalSettings,
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService,
            _userService,
            _pushNotificationService,
            Substitute.For<ILogger<CreatePremiumCloudHostedSubscriptionCommand>>(),
            _pricingClient,
            _hasPaymentMethodQuery,
            _updatePaymentMethodCommand,
            _subscriptionDiscountService);
    }

    #region Helper Methods

    private static PremiumSubscriptionPurchase CreateSubscriptionPurchase(
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb = 0,
        string? coupon = null)
    {
        return new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = additionalStorageGb,
            Coupon = coupon
        };
    }

    private static StripeCustomer CreateMockCustomer(string customerId = "cust_123", string country = "US", string postalCode = "12345")
    {
        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = customerId;
        mockCustomer.Address = new Address { Country = country, PostalCode = postalCode };
        mockCustomer.Metadata = new Dictionary<string, string>();
        return mockCustomer;
    }

    private static StripeSubscription CreateMockActiveSubscription(string subscriptionId = "sub_123")
    {
        var mockSubscription = Substitute.For<StripeSubscription>();
        mockSubscription.Id = subscriptionId;
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
        return mockSubscription;
    }

    #endregion

    [Theory, BitAutoData]
    public async Task Run_UserAlreadyPremium_ReturnsBadRequest(
        User user,
        PremiumSubscriptionPurchase subscriptionPurchase)
    {
        // Arrange
        user.Premium = true;

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Already a premium user.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_NegativeStorageAmount_ReturnsBadRequest(
        User user,
        PremiumSubscriptionPurchase subscriptionPurchase)
    {
        // Arrange
        user.Premium = false;
        subscriptionPurchase = subscriptionPurchase with { AdditionalStorageGb = -1 };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _stripeAdapter.ListSetupIntentsAsync(Arg.Any<SetupIntentListOptions>()).Returns(Task.FromResult(new List<SetupIntent> { mockSetupIntent }));
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = additionalStorage,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        // Verify that update payment method was called (new behavior for credit purchase case)
        await _updatePaymentMethodCommand.Received(1).Run(user, paymentMethod, billingAddress);
        // Verify GetCustomerOrThrow was called after updating payment method
        await _subscriberService.Received(1).GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>());
        // Verify no new customer was created
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        // Verify subscription was created
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.CreateBraintreeCustomer(Arg.Any<User>(), Arg.Any<string>()).Returns("bt_customer_123");

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        _stripeAdapter.ListSetupIntentsAsync(Arg.Any<SetupIntentListOptions>())
            .Returns(Task.FromResult(new List<SetupIntent>())); // Empty list - no setup intent found

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceUpdateOptions>()).Returns(mockInvoice);

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0,
            Coupon = null
        };

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        //Assert
        Assert.True(result.IsT3); // Assuming T3 is the Unhandled result
        Assert.IsType<BillingException>(result.AsT3.Exception);
        // Verify no customer was created or subscription attempted
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.DidNotReceive().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = additionalStorage,
            Coupon = null
        };

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal((short)3, user.MaxStorageGb); // 1 (provided) + 2 (additional) = 3
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithCanceledSubscription_AllowsResubscribe(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true; // User still has Premium flag set
        user.GatewayCustomerId = "existing_customer_123";
        user.GatewaySubscriptionId = "sub_canceled_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var existingCanceledSubscription = Substitute.For<StripeSubscription>();
        existingCanceledSubscription.Id = "sub_canceled_123";
        existingCanceledSubscription.Status = "canceled"; // Terminal status

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var newSubscription = Substitute.For<StripeSubscription>();
        newSubscription.Id = "sub_new_123";
        newSubscription.Status = "active";
        newSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId).Returns(existingCanceledSubscription);
        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(true);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        // Act
        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0
        };
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0); // Should succeed, not return "Already a premium user"
        Assert.True(user.Premium);
        Assert.Equal(newSubscription.Id, user.GatewaySubscriptionId);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithIncompleteExpiredSubscription_AllowsResubscribe(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true; // User still has Premium flag set
        user.GatewayCustomerId = "existing_customer_123";
        user.GatewaySubscriptionId = "sub_incomplete_expired_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var existingExpiredSubscription = Substitute.For<StripeSubscription>();
        existingExpiredSubscription.Id = "sub_incomplete_expired_123";
        existingExpiredSubscription.Status = "incomplete_expired"; // Terminal status

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var newSubscription = Substitute.For<StripeSubscription>();
        newSubscription.Id = "sub_new_123";
        newSubscription.Status = "active";
        newSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId).Returns(existingExpiredSubscription);
        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(true);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        // Act
        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0
        };
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0); // Should succeed, not return "Already a premium user"
        Assert.True(user.Premium);
        Assert.Equal(newSubscription.Id, user.GatewaySubscriptionId);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithActiveSubscription_PremiumTrue_ReturnsBadRequest(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_active_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;

        var existingActiveSubscription = Substitute.For<StripeSubscription>();
        existingActiveSubscription.Id = "sub_active_123";
        existingActiveSubscription.Status = "active"; // NOT a terminal status

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId).Returns(existingActiveSubscription);

        // Act
        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0
        };
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Already a premium user.", badRequest.Response);
        // Verify no subscription creation was attempted
        await _stripeAdapter.DidNotReceive().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_SubscriptionFetchThrows_ProceedsWithCreation(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_customer_123";
        user.GatewaySubscriptionId = "sub_nonexistent_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        // Simulate Stripe exception when fetching subscription (e.g., subscription doesn't exist)
        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId)
            .Returns<StripeSubscription>(_ => throw new Stripe.StripeException("Subscription not found"));

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var newSubscription = Substitute.For<StripeSubscription>();
        newSubscription.Id = "sub_new_123";
        newSubscription.Status = "active";
        newSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(true);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        // Act
        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0
        };
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert - Should proceed successfully despite the exception
        Assert.True(result.IsT0);
        Assert.True(user.Premium);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_ResubscribeWithTerminalSubscription_UpdatesPaymentMethod(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewayCustomerId = "existing_customer_123";
        user.GatewaySubscriptionId = "sub_canceled_123";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "new_card_token_456";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var existingCanceledSubscription = Substitute.For<StripeSubscription>();
        existingCanceledSubscription.Id = "sub_canceled_123";
        existingCanceledSubscription.Status = "canceled"; // Terminal status

        var mockCustomer = Substitute.For<StripeCustomer>();
        mockCustomer.Id = "existing_customer_123";
        mockCustomer.Address = new Address { Country = "US", PostalCode = "12345" };
        mockCustomer.Metadata = new Dictionary<string, string>();

        var newSubscription = Substitute.For<StripeSubscription>();
        newSubscription.Id = "sub_new_123";
        newSubscription.Status = "active";
        newSubscription.Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }
            ]
        };

        MaskedPaymentMethod mockMaskedPaymentMethod = new MaskedCard
        {
            Brand = "visa",
            Last4 = "4567",
            Expiration = "12/2026"
        };

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId).Returns(existingCanceledSubscription);
        _hasPaymentMethodQuery.Run(Arg.Any<User>()).Returns(true); // Has old payment method
        _updatePaymentMethodCommand.Run(Arg.Any<User>(), Arg.Any<TokenizedPaymentMethod>(), Arg.Any<BillingAddress>())
            .Returns(mockMaskedPaymentMethod);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        // Act
        var subscriptionPurchase = new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = 0
        };
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        // Verify payment method was updated because of terminal subscription
        await _updatePaymentMethodCommand.Received(1).Run(user, paymentMethod, billingAddress);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidCoupon_AppliesCouponSuccessfully(
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

        var subscriptionPurchase = CreateSubscriptionPurchase(paymentMethod, billingAddress, coupon: "VALID_COUPON");
        var mockCustomer = CreateMockCustomer();
        var mockSubscription = CreateMockActiveSubscription();

        _subscriptionDiscountService.ValidateDiscountForUserAsync(user, "VALID_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(true);
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(user, "VALID_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
            opts.Discounts != null &&
            opts.Discounts.Count == 1 &&
            opts.Discounts[0].Coupon == "VALID_COUPON"));
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_InvalidCoupon_IgnoresCouponAndProceeds(
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

        var subscriptionPurchase = CreateSubscriptionPurchase(paymentMethod, billingAddress, coupon: "INVALID_COUPON");
        var mockCustomer = CreateMockCustomer();
        var mockSubscription = CreateMockActiveSubscription();

        _subscriptionDiscountService.ValidateDiscountForUserAsync(user, "INVALID_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(false);
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(user, "INVALID_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
            opts.Discounts == null || opts.Discounts.Count == 0));
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_UserNotEligibleForCoupon_IgnoresCouponAndProceeds(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "existing_customer_123";
        user.GatewaySubscriptionId = null;
        user.Email = "test@example.com";
        paymentMethod.Type = TokenizablePaymentMethodType.Card;
        paymentMethod.Token = "card_token_123";

        var subscriptionPurchase = CreateSubscriptionPurchase(paymentMethod, billingAddress, coupon: "NEW_USER_ONLY_COUPON");
        var mockCustomer = CreateMockCustomer();
        var mockSubscription = CreateMockActiveSubscription();

        // User has previous subscriptions, so they're not eligible
        _subscriptionDiscountService.ValidateDiscountForUserAsync(user, "NEW_USER_ONLY_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(false);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(user, "NEW_USER_ONLY_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions);
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
            opts.Discounts == null || opts.Discounts.Count == 0));
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task Run_CouponWithWhitespace_TrimsCouponCode(
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

        var subscriptionPurchase = CreateSubscriptionPurchase(paymentMethod, billingAddress, coupon: "  WHITESPACE_COUPON  ");
        var mockCustomer = CreateMockCustomer();
        var mockSubscription = CreateMockActiveSubscription();

        _subscriptionDiscountService.ValidateDiscountForUserAsync(user, "WHITESPACE_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(true);
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(mockCustomer);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(mockCustomer);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(mockSubscription);
        _subscriberService.GetCustomerOrThrow(Arg.Any<User>(), Arg.Any<CustomerGetOptions>()).Returns(mockCustomer);

        // Act
        var result = await _command.Run(user, subscriptionPurchase);

        // Assert
        Assert.True(result.IsT0);
        // Verify the coupon was trimmed before validation
        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(user, "WHITESPACE_COUPON", DiscountAudienceType.UserHasNoPreviousSubscriptions);
        // Verify the coupon was trimmed before passing to Stripe
        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
            opts.Discounts != null &&
            opts.Discounts.Count == 1 &&
            opts.Discounts[0].Coupon == "WHITESPACE_COUPON"));
    }

}
