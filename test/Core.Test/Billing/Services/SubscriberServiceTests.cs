using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;
using Address = Stripe.Address;
using Customer = Stripe.Customer;
using PaymentMethod = Stripe.PaymentMethod;
using Subscription = Stripe.Subscription;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class SubscriberServiceTests
{
    #region CancelSubscription

    [Theory, BitAutoData]
    public async Task CancelSubscription_SubscriptionInactive_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription
        {
            Status = "canceled"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.CancelSubscription(organization, new OffboardingSurveyResponse(), false));

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToOrganization_UpdatesSubscription_CancelSubscriptionImmediately(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", "organization_id" }
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, true);

        await stripeAdapter
            .Received(1)
            .SubscriptionUpdateAsync(subscriptionId, Arg.Is<SubscriptionUpdateOptions>(
                options => options.Metadata["cancellingUserId"] == userId.ToString()));

        await stripeAdapter
            .Received(1)
            .SubscriptionCancelAsync(subscriptionId, Arg.Is<SubscriptionCancelOptions>(options =>
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToUser_CancelSubscriptionImmediately(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "userId", "user_id" }
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, true);

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

        await stripeAdapter
            .Received(1)
            .SubscriptionCancelAsync(subscriptionId, Arg.Is<SubscriptionCancelOptions>(options =>
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_DoNotCancelImmediately_UpdateSubscriptionToCancelAtEndOfPeriod(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        organization.ExpirationDate = DateTime.UtcNow.AddDays(5);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, false);

        await stripeAdapter
            .Received(1)
            .SubscriptionUpdateAsync(subscriptionId, Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAtPeriodEnd == true &&
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason &&
                options.Metadata["cancellingUserId"] == userId.ToString()));

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>()); ;
    }

    #endregion

    #region GetCustomer

    [Theory, BitAutoData]
    public async Task GetCustomer_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetCustomer(null));

    [Theory, BitAutoData]
    public async Task GetCustomer_NoGatewayCustomerId_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_NoCustomer_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ReturnsNull();

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_StripeException_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ThrowsAsync<StripeException>();

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .Returns(customer);

        var gotCustomer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Equivalent(customer, gotCustomer);
    }

    #endregion

    #region GetCustomerOrThrow

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetCustomerOrThrow(null));

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NoGatewayCustomerId_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await ThrowsBillingExceptionAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NoCustomer_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ReturnsNull();

        await ThrowsBillingExceptionAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_StripeException_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ThrowsAsync(stripeException);

        await ThrowsBillingExceptionAsync(
            async () => await sutProvider.Sut.GetCustomerOrThrow(organization),
            message: "An error occurred while trying to retrieve a Stripe customer",
            innerException: stripeException);
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .Returns(customer);

        var gotCustomer = await sutProvider.Sut.GetCustomerOrThrow(organization);

        Assert.Equivalent(customer, gotCustomer);
    }

    #endregion

    #region GetPaymentMethod
    [Theory, BitAutoData]
    public async Task GetPaymentMethod_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GetPaymentSource(null));

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Braintree_NoDefaultPaymentMethod_ReturnsNull(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        var customer = new Customer
        {
            Id = provider.GatewayCustomerId,
            Metadata = new Dictionary<string, string>
            {
                [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var (_, customerGateway, _) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        braintreeCustomer.Id.Returns(braintreeCustomerId);

        braintreeCustomer.PaymentMethods.Returns([]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Null(paymentMethod);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Braintree_PayPalAccount_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        var customer = new Customer
        {
            Id = provider.GatewayCustomerId,
            Metadata = new Dictionary<string, string>
            {
                [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var (_, customerGateway, _) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        braintreeCustomer.Id.Returns(braintreeCustomerId);

        var payPalAccount = Substitute.For<PayPalAccount>();

        payPalAccount.IsDefault.Returns(true);

        payPalAccount.Email.Returns("a@example.com");

        braintreeCustomer.PaymentMethods.Returns([payPalAccount]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.PayPal, paymentMethod.Type);
        Assert.Equal("a@example.com", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    // TODO: Determine if we need to test Braintree.CreditCard

    // TODO: Determine if we need to test Braintree.UsBankAccount

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_BankAccountPaymentMethod_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = StripeConstants.PaymentMethodTypes.USBankAccount,
                    UsBankAccount = new PaymentMethodUsBankAccount
                    {
                        BankName = "Chase",
                        Last4 = "9999"
                    }
                }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.BankAccount, paymentMethod.Type);
        Assert.Equal("Chase, *9999", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_CardPaymentMethod_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = StripeConstants.PaymentMethodTypes.Card,
                    Card = new PaymentMethodCard
                    {
                        Brand = "Visa",
                        Last4 = "9999",
                        ExpMonth = 9,
                        ExpYear = 2028
                    }
                }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.Card, paymentMethod.Type);
        Assert.Equal("VISA, *9999, 09/2028", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_SetupIntentForBankAccount_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            Id = provider.GatewayCustomerId
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var setupIntent = new SetupIntent
        {
            Id = "setup_intent_id",
            Status = "requires_action",
            NextAction = new SetupIntentNextAction
            {
                VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
            },
            PaymentMethod = new PaymentMethod
            {
                UsBankAccount = new PaymentMethodUsBankAccount
                {
                    BankName = "Chase",
                    Last4 = "9999"
                }
            }
        };

        sutProvider.GetDependency<ISetupIntentCache>().Get(provider.Id).Returns(setupIntent.Id);

        sutProvider.GetDependency<IStripeAdapter>().SetupIntentGet(setupIntent.Id, Arg.Is<SetupIntentGetOptions>(
            options => options.Expand.Contains("payment_method"))).Returns(setupIntent);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.BankAccount, paymentMethod.Type);
        Assert.Equal("Chase, *9999", paymentMethod.Description);
        Assert.True(paymentMethod.NeedsVerification);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_LegacyBankAccount_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            DefaultSource = new BankAccount
            {
                Status = "verified",
                BankName = "Chase",
                Last4 = "9999"
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.BankAccount, paymentMethod.Type);
        Assert.Equal("Chase, *9999 - Verified", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_LegacyCard_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            DefaultSource = new Card
            {
                Brand = "Visa",
                Last4 = "9999",
                ExpMonth = 9,
                ExpYear = 2028
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.Card, paymentMethod.Type);
        Assert.Equal("VISA, *9999, 09/2028", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Stripe_LegacySourceCard_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            DefaultSource = new Source
            {
                Card = new SourceCard
                {
                    Brand = "Visa",
                    Last4 = "9999",
                    ExpMonth = 9,
                    ExpYear = 2028
                }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId,
                Arg.Is<CustomerGetOptions>(
                    options => options.Expand.Contains("default_source") &&
                               options.Expand.Contains("invoice_settings.default_payment_method")))
            .Returns(customer);

        var paymentMethod = await sutProvider.Sut.GetPaymentSource(provider);

        Assert.Equal(PaymentMethodType.Card, paymentMethod.Type);
        Assert.Equal("VISA, *9999, 09/2028", paymentMethod.Description);
        Assert.False(paymentMethod.NeedsVerification);
    }

    #endregion

    #region GetSubscription
    [Theory, BitAutoData]
    public async Task GetSubscription_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscription(null));

    [Theory, BitAutoData]
    public async Task GetSubscription_NoGatewaySubscriptionId_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_NoSubscription_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_StripeException_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ThrowsAsync<StripeException>();

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion

    #region GetSubscriptionOrThrow
    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(null));

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NoGatewaySubscriptionId_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsBillingExceptionAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NoSubscription_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsBillingExceptionAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_StripeException_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ThrowsAsync(stripeException);

        await ThrowsBillingExceptionAsync(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization),
            message: "An error occurred while trying to retrieve a Stripe subscription",
            innerException: stripeException);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscriptionOrThrow(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion

    #region GetTaxInformation

    [Theory, BitAutoData]
    public async Task GetTaxInformation_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GetTaxInformation(null));

    [Theory, BitAutoData]
    public async Task GetTaxInformation_NullAddress_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(new Customer());

        var taxInformation = await sutProvider.Sut.GetTaxInformation(organization);

        Assert.Null(taxInformation);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformation_Success(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var address = new Address
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Example St.",
            Line2 = "Unit 1",
            City = "Example Town",
            State = "NY"
        };

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(new Customer
            {
                Address = address,
                TaxIds = new StripeList<TaxId>
                {
                    Data = [new TaxId { Value = "tax_id" }]
                }
            });

        var taxInformation = await sutProvider.Sut.GetTaxInformation(organization);

        Assert.NotNull(taxInformation);
        Assert.Equal(address.Country, taxInformation.Country);
        Assert.Equal(address.PostalCode, taxInformation.PostalCode);
        Assert.Equal("tax_id", taxInformation.TaxId);
        Assert.Equal(address.Line1, taxInformation.Line1);
        Assert.Equal(address.Line2, taxInformation.Line2);
        Assert.Equal(address.City, taxInformation.City);
        Assert.Equal(address.State, taxInformation.State);
    }

    #endregion

    #region RemovePaymentMethod
    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.RemovePaymentSource(null));

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_NoCustomer_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";

        var stripeCustomer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (braintreeGateway, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        customerGateway.FindAsync(braintreeCustomerId).ReturnsNull();

        braintreeGateway.Customer.Returns(customerGateway);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.RemovePaymentSource(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<string>(), Arg.Any<CustomerRequest>());

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_NoPaymentMethod_NoOp(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";

        var stripeCustomer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        braintreeCustomer.PaymentMethods.Returns([]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        await sutProvider.Sut.RemovePaymentSource(organization);

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.DidNotReceiveWithAnyArgs().UpdateAsync(Arg.Any<string>(), Arg.Any<CustomerRequest>());

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_CustomerUpdateFails_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var stripeCustomer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        var paymentMethod = Substitute.For<Braintree.PaymentMethod>();
        paymentMethod.Token.Returns(braintreePaymentMethodToken);
        paymentMethod.IsDefault.Returns(true);

        braintreeCustomer.PaymentMethods.Returns([
            paymentMethod
        ]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var updateBraintreeCustomerResult = Substitute.For<Result<Braintree.Customer>>();
        updateBraintreeCustomerResult.IsSuccess().Returns(false);

        customerGateway.UpdateAsync(
                braintreeCustomerId,
                Arg.Is<CustomerRequest>(request => request.DefaultPaymentMethodToken == null))
            .Returns(updateBraintreeCustomerResult);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.RemovePaymentSource(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(paymentMethod.Token);

        await customerGateway.DidNotReceive().UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_PaymentMethodDeleteFails_RollBack_ThrowsBillingException(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var stripeCustomer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        var paymentMethod = Substitute.For<Braintree.PaymentMethod>();
        paymentMethod.Token.Returns(braintreePaymentMethodToken);
        paymentMethod.IsDefault.Returns(true);

        braintreeCustomer.PaymentMethods.Returns([
            paymentMethod
        ]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var updateBraintreeCustomerResult = Substitute.For<Result<Braintree.Customer>>();
        updateBraintreeCustomerResult.IsSuccess().Returns(true);

        customerGateway.UpdateAsync(braintreeCustomerId, Arg.Any<CustomerRequest>())
            .Returns(updateBraintreeCustomerResult);

        var deleteBraintreePaymentMethodResult = Substitute.For<Result<Braintree.PaymentMethod>>();
        deleteBraintreePaymentMethodResult.IsSuccess().Returns(false);

        paymentMethodGateway.DeleteAsync(paymentMethod.Token).Returns(deleteBraintreePaymentMethodResult);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.RemovePaymentSource(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.Received(1).DeleteAsync(paymentMethod.Token);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Stripe_Legacy_RemovesSources(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string bankAccountId = "bank_account_id";
        const string cardId = "card_id";

        var sources = new List<IPaymentSource>
        {
            new BankAccount { Id = bankAccountId }, new Card { Id = cardId }
        };

        var stripeCustomer = new Customer { Sources = new StripeList<IPaymentSource> { Data = sources } };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        stripeAdapter
            .PaymentMethodListAutoPagingAsync(Arg.Any<PaymentMethodListOptions>())
            .Returns(GetPaymentMethodsAsync(new List<Stripe.PaymentMethod>()));

        await sutProvider.Sut.RemovePaymentSource(organization);

        await stripeAdapter.Received(1).BankAccountDeleteAsync(stripeCustomer.Id, bankAccountId);

        await stripeAdapter.Received(1).CardDeleteAsync(stripeCustomer.Id, cardId);

        await stripeAdapter.DidNotReceiveWithAnyArgs()
            .PaymentMethodDetachAsync(Arg.Any<string>(), Arg.Any<PaymentMethodDetachOptions>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Stripe_DetachesPaymentMethods(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string bankAccountId = "bank_account_id";
        const string cardId = "card_id";

        var sources = new List<IPaymentSource>();

        var stripeCustomer = new Customer { Sources = new StripeList<IPaymentSource> { Data = sources } };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        stripeAdapter
            .PaymentMethodListAutoPagingAsync(Arg.Any<PaymentMethodListOptions>())
            .Returns(GetPaymentMethodsAsync(new List<Stripe.PaymentMethod>
            {
                new ()
                {
                    Id = bankAccountId
                },
                new ()
                {
                    Id = cardId
                }
            }));

        await sutProvider.Sut.RemovePaymentSource(organization);

        await stripeAdapter.DidNotReceiveWithAnyArgs().BankAccountDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.DidNotReceiveWithAnyArgs().CardDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(bankAccountId);

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(cardId);
    }

    private static async IAsyncEnumerable<Stripe.PaymentMethod> GetPaymentMethodsAsync(
        IEnumerable<Stripe.PaymentMethod> paymentMethods)
    {
        foreach (var paymentMethod in paymentMethods)
        {
            yield return paymentMethod;
        }

        await Task.CompletedTask;
    }

    private static (IBraintreeGateway, ICustomerGateway, IPaymentMethodGateway) SetupBraintree(
        IBraintreeGateway braintreeGateway)
    {
        var customerGateway = Substitute.For<ICustomerGateway>();
        var paymentMethodGateway = Substitute.For<IPaymentMethodGateway>();

        braintreeGateway.Customer.Returns(customerGateway);
        braintreeGateway.PaymentMethod.Returns(paymentMethodGateway);

        return (braintreeGateway, customerGateway, paymentMethodGateway);
    }
    #endregion

    #region UpdatePaymentMethod

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.UpdatePaymentSource(null, null));

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_NullTokenizedPaymentMethod_ThrowsArgumentNullException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.UpdatePaymentSource(provider, null));

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_NoToken_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.Card, null)));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_UnsupportedPaymentMethod_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.BitPay, "TOKEN")));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_BankAccount_IncorrectNumberOfSetupIntentsForToken_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options => options.PaymentMethod == "TOKEN"))
            .Returns([new SetupIntent(), new SetupIntent()]);

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.BankAccount, "TOKEN")));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_BankAccount_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = "braintree_customer_id"
                }
            });

        var matchingSetupIntent = new SetupIntent { Id = "setup_intent_1" };

        stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options => options.PaymentMethod == "TOKEN"))
            .Returns([matchingSetupIntent]);

        stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options => options.Customer == provider.GatewayCustomerId))
            .Returns([
                new SetupIntent { Id = "setup_intent_2", Status = "requires_payment_method" },
                new SetupIntent { Id = "setup_intent_3", Status = "succeeded" }
            ]);

        stripeAdapter.CustomerListPaymentMethods(provider.GatewayCustomerId).Returns([
            new PaymentMethod { Id = "payment_method_1" }
        ]);

        await sutProvider.Sut.UpdatePaymentSource(provider,
            new TokenizedPaymentSource(PaymentMethodType.BankAccount, "TOKEN"));

        await sutProvider.GetDependency<ISetupIntentCache>().Received(1).Set(provider.Id, "setup_intent_1");

        await stripeAdapter.Received(1).SetupIntentCancel("setup_intent_2",
            Arg.Is<SetupIntentCancelOptions>(options => options.CancellationReason == "abandoned"));

        await stripeAdapter.Received(1).PaymentMethodDetachAsync("payment_method_1");

        await stripeAdapter.Received(1).CustomerUpdateAsync(provider.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options => options.Metadata[Core.Billing.Utilities.BraintreeCustomerIdKey] == null));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Card_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = "braintree_customer_id"
                }
            });

        stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options => options.Customer == provider.GatewayCustomerId))
            .Returns([
                new SetupIntent { Id = "setup_intent_2", Status = "requires_payment_method" },
                new SetupIntent { Id = "setup_intent_3", Status = "succeeded" }
            ]);

        stripeAdapter.CustomerListPaymentMethods(provider.GatewayCustomerId).Returns([
            new PaymentMethod { Id = "payment_method_1" }
        ]);

        await sutProvider.Sut.UpdatePaymentSource(provider,
            new TokenizedPaymentSource(PaymentMethodType.Card, "TOKEN"));

        await stripeAdapter.Received(1).SetupIntentCancel("setup_intent_2",
            Arg.Is<SetupIntentCancelOptions>(options => options.CancellationReason == "abandoned"));

        await stripeAdapter.Received(1).PaymentMethodDetachAsync("payment_method_1");

        await stripeAdapter.Received(1).PaymentMethodAttachAsync("TOKEN", Arg.Is<PaymentMethodAttachOptions>(
            options => options.Customer == provider.GatewayCustomerId));

        await stripeAdapter.Received(1).CustomerUpdateAsync(provider.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options =>
                options.InvoiceSettings.DefaultPaymentMethod == "TOKEN" &&
                options.Metadata[Core.Billing.Utilities.BraintreeCustomerIdKey] == null));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_NullCustomer_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
                }
            });

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        customerGateway.FindAsync(braintreeCustomerId).ReturnsNull();

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN")));

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().CreateAsync(Arg.Any<PaymentMethodRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_ReplacePaymentMethod_CreatePaymentMethodFails_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
                }
            });

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var customer = Substitute.For<Braintree.Customer>();

        customer.Id.Returns(braintreeCustomerId);

        customerGateway.FindAsync(braintreeCustomerId).Returns(customer);

        var createPaymentMethodResult = Substitute.For<Result<Braintree.PaymentMethod>>();

        createPaymentMethodResult.IsSuccess().Returns(false);

        paymentMethodGateway.CreateAsync(Arg.Is<PaymentMethodRequest>(
                options => options.CustomerId == braintreeCustomerId && options.PaymentMethodNonce == "TOKEN"))
            .Returns(createPaymentMethodResult);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN")));

        await customerGateway.DidNotReceiveWithAnyArgs().UpdateAsync(Arg.Any<string>(), Arg.Any<CustomerRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_ReplacePaymentMethod_UpdateCustomerFails_DeletePaymentMethod_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
                }
            });

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var customer = Substitute.For<Braintree.Customer>();

        customer.Id.Returns(braintreeCustomerId);

        customerGateway.FindAsync(braintreeCustomerId).Returns(customer);

        var createPaymentMethodResult = Substitute.For<Result<Braintree.PaymentMethod>>();

        var createdPaymentMethod = Substitute.For<Braintree.PaymentMethod>();

        createdPaymentMethod.Token.Returns("TOKEN");

        createPaymentMethodResult.IsSuccess().Returns(true);

        createPaymentMethodResult.Target.Returns(createdPaymentMethod);

        paymentMethodGateway.CreateAsync(Arg.Is<PaymentMethodRequest>(
                options => options.CustomerId == braintreeCustomerId && options.PaymentMethodNonce == "TOKEN"))
            .Returns(createPaymentMethodResult);

        var updateCustomerResult = Substitute.For<Result<Braintree.Customer>>();

        updateCustomerResult.IsSuccess().Returns(false);

        customerGateway.UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(options =>
                options.DefaultPaymentMethodToken == createPaymentMethodResult.Target.Token))
            .Returns(updateCustomerResult);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.UpdatePaymentSource(provider, new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN")));

        await paymentMethodGateway.Received(1).DeleteAsync(createPaymentMethodResult.Target.Token);
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_ReplacePaymentMethod_Success(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId,
                Metadata = new Dictionary<string, string>
                {
                    [Core.Billing.Utilities.BraintreeCustomerIdKey] = braintreeCustomerId
                }
            });

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var customer = Substitute.For<Braintree.Customer>();

        var existingPaymentMethod = Substitute.For<Braintree.PaymentMethod>();

        existingPaymentMethod.Token.Returns("OLD_TOKEN");

        existingPaymentMethod.IsDefault.Returns(true);

        customer.PaymentMethods.Returns([existingPaymentMethod]);

        customer.Id.Returns(braintreeCustomerId);

        customerGateway.FindAsync(braintreeCustomerId).Returns(customer);

        var createPaymentMethodResult = Substitute.For<Result<Braintree.PaymentMethod>>();

        var updatedPaymentMethod = Substitute.For<Braintree.PaymentMethod>();

        updatedPaymentMethod.Token.Returns("TOKEN");

        createPaymentMethodResult.IsSuccess().Returns(true);

        createPaymentMethodResult.Target.Returns(updatedPaymentMethod);

        paymentMethodGateway.CreateAsync(Arg.Is<PaymentMethodRequest>(
                options => options.CustomerId == braintreeCustomerId && options.PaymentMethodNonce == "TOKEN"))
            .Returns(createPaymentMethodResult);

        var updateCustomerResult = Substitute.For<Result<Braintree.Customer>>();

        updateCustomerResult.IsSuccess().Returns(true);

        customerGateway.UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(options =>
                options.DefaultPaymentMethodToken == createPaymentMethodResult.Target.Token))
            .Returns(updateCustomerResult);

        var deletePaymentMethodResult = Substitute.For<Result<Braintree.PaymentMethod>>();

        deletePaymentMethodResult.IsSuccess().Returns(true);

        paymentMethodGateway.DeleteAsync(existingPaymentMethod.Token).Returns(deletePaymentMethodResult);

        await sutProvider.Sut.UpdatePaymentSource(provider,
            new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN"));

        await paymentMethodGateway.Received(1).DeleteAsync(existingPaymentMethod.Token);
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_CreateCustomer_CustomerUpdateFails_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId
            });

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new Settings.GlobalSettings.BaseServiceUriSettings(new Settings.GlobalSettings())
            {
                CloudRegion = "US"
            });

        var (_, customerGateway, _) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var createCustomerResult = Substitute.For<Result<Braintree.Customer>>();

        createCustomerResult.IsSuccess().Returns(false);

        customerGateway.CreateAsync(Arg.Is<CustomerRequest>(
                options =>
                    options.Id == braintreeCustomerId &&
                    options.CustomFields[provider.BraintreeIdField()] == provider.Id.ToString() &&
                    options.CustomFields[provider.BraintreeCloudRegionField()] == "US" &&
                    options.Email == provider.BillingEmailAddress() &&
                    options.PaymentMethodNonce == "TOKEN"))
            .Returns(createCustomerResult);

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.UpdatePaymentSource(provider,
                new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN")));

        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Braintree_CreateCustomer_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "braintree_customer_id";

        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer
            {
                Id = provider.GatewayCustomerId
            });

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new Settings.GlobalSettings.BaseServiceUriSettings(new Settings.GlobalSettings())
            {
                CloudRegion = "US"
            });

        var (_, customerGateway, _) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var createCustomerResult = Substitute.For<Result<Braintree.Customer>>();

        var createdCustomer = Substitute.For<Braintree.Customer>();

        createdCustomer.Id.Returns(braintreeCustomerId);

        createCustomerResult.IsSuccess().Returns(true);

        createCustomerResult.Target.Returns(createdCustomer);

        customerGateway.CreateAsync(Arg.Is<CustomerRequest>(
                options =>
                    options.CustomFields[provider.BraintreeIdField()] == provider.Id.ToString() &&
                    options.CustomFields[provider.BraintreeCloudRegionField()] == "US" &&
                    options.Email == provider.BillingEmailAddress() &&
                    options.PaymentMethodNonce == "TOKEN"))
            .Returns(createCustomerResult);

        await sutProvider.Sut.UpdatePaymentSource(provider,
            new TokenizedPaymentSource(PaymentMethodType.PayPal, "TOKEN"));

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CustomerUpdateAsync(provider.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(
                options => options.Metadata[Core.Billing.Utilities.BraintreeCustomerIdKey] == braintreeCustomerId));
    }

    #endregion

    #region UpdateTaxInformation

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(
        () => sutProvider.Sut.UpdateTaxInformation(null, null));

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_NullTaxInformation_ThrowsArgumentNullException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.UpdateTaxInformation(provider, null));

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_NonUser_MakesCorrectInvocations(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var customer = new Customer { Id = provider.GatewayCustomerId, TaxIds = new StripeList<TaxId> { Data = [new TaxId { Id = "tax_id_1" }] } };

        stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId, Arg.Is<CustomerGetOptions>(
            options => options.Expand.Contains("tax_ids"))).Returns(customer);

        var taxInformation = new TaxInformation(
            "US",
            "12345",
            "123456789",
            "123 Example St.",
            null,
            "Example Town",
            "NY");

        await sutProvider.Sut.UpdateTaxInformation(provider, taxInformation);

        await stripeAdapter.Received(1).CustomerUpdateAsync(provider.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options =>
                options.Address.Country == taxInformation.Country &&
                options.Address.PostalCode == taxInformation.PostalCode &&
                options.Address.Line1 == taxInformation.Line1 &&
                options.Address.Line2 == taxInformation.Line2 &&
                options.Address.City == taxInformation.City &&
                options.Address.State == taxInformation.State));

        await stripeAdapter.Received(1).TaxIdDeleteAsync(provider.GatewayCustomerId, "tax_id_1");

        await stripeAdapter.Received(1).TaxIdCreateAsync(provider.GatewayCustomerId, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == "us_ein" &&
                       options.Value == taxInformation.TaxId));
    }

    #endregion

    #region VerifyBankAccount

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_NoSetupIntentId_ThrowsBillingException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider) => await ThrowsBillingExceptionAsync(() => sutProvider.Sut.VerifyBankAccount(provider, ""));

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_MakesCorrectInvocations(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        const string descriptorCode = "SM1234";

        var setupIntent = new SetupIntent
        {
            Id = "setup_intent_id",
            PaymentMethodId = "payment_method_id"
        };

        sutProvider.GetDependency<ISetupIntentCache>().Get(provider.Id).Returns(setupIntent.Id);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SetupIntentGet(setupIntent.Id).Returns(setupIntent);

        await sutProvider.Sut.VerifyBankAccount(provider, descriptorCode);

        await stripeAdapter.Received(1).SetupIntentVerifyMicroDeposit(setupIntent.Id,
            Arg.Is<SetupIntentVerifyMicrodepositsOptions>(
                options => options.DescriptorCode == descriptorCode));

        await stripeAdapter.Received(1).PaymentMethodAttachAsync(setupIntent.PaymentMethodId,
            Arg.Is<PaymentMethodAttachOptions>(
                options => options.Customer == provider.GatewayCustomerId));

        await stripeAdapter.Received(1).CustomerUpdateAsync(provider.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options => options.InvoiceSettings.DefaultPaymentMethod == setupIntent.PaymentMethodId));
    }

    #endregion
}
