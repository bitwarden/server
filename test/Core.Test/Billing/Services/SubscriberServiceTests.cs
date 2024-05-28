using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Enums;
using Bit.Core.Services;
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
    public async Task CancelSubscription_SubscriptionInactive_ContactSupport(
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

        await ThrowsContactSupportAsync(() =>
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

    #region GetAccountCredit

    [Theory, BitAutoData]
    public async Task GetAccountCredit_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GetAccountCredit(null));

    [Theory, BitAutoData]
    public async Task GetAccountCredit_NoCustomer_ReturnsNull(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .ReturnsNull();

        var accountCredit = await sutProvider.Sut.GetAccountCredit(provider);

        Assert.Null(accountCredit);
    }

    [Theory, BitAutoData]
    public async Task GetAccountCredit_Succeeds(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer { Balance = -2500 });

        var accountCredit = await sutProvider.Sut.GetAccountCredit(provider);

        Assert.Equal(25, accountCredit);
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
    public async Task GetCustomerOrThrow_NoGatewayCustomerId_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NoCustomer_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_StripeException_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ThrowsAsync(stripeException);

        await ThrowsContactSupportAsync(
            async () => await sutProvider.Sut.GetCustomerOrThrow(organization),
            "An error occurred while trying to retrieve a Stripe Customer",
            stripeException);
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
    // TODO: Braintree remains completely un-mockable.

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GetPaymentMethod(null));

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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

        var paymentMethod = await sutProvider.Sut.GetPaymentMethod(provider);

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
    public async Task GetSubscriptionOrThrow_NoGatewaySubscriptionId_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NoSubscription_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_StripeException_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ThrowsAsync(stripeException);

        await ThrowsContactSupportAsync(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization),
            "An error occurred while trying to retrieve a Stripe Subscription",
            stripeException);
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
    public async Task GetTaxInformation_CustomerHasNoAddress_ReturnsNull(
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
    public async Task RemovePaymentMethod_NullSubscriber_ArgumentNullException(
        SutProvider<SubscriberService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.RemovePaymentMethod(null));

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_NoCustomer_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";

        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var (braintreeGateway, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        customerGateway.FindAsync(braintreeCustomerId).ReturnsNull();

        braintreeGateway.Customer.Returns(customerGateway);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

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

        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();

        braintreeCustomer.PaymentMethods.Returns([]);

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.DidNotReceiveWithAnyArgs().UpdateAsync(Arg.Any<string>(), Arg.Any<CustomerRequest>());

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_CustomerUpdateFails_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();
        braintreeCustomer.Id.Returns(braintreeCustomerId);

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

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(paymentMethod.Token);

        await customerGateway.DidNotReceive().UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_Success(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var (_, customerGateway, paymentMethodGateway) = SetupBraintree(sutProvider.GetDependency<IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<Braintree.Customer>();
        braintreeCustomer.Id.Returns(braintreeCustomerId);

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
        deleteBraintreePaymentMethodResult.IsSuccess().Returns(true);

        paymentMethodGateway.DeleteAsync(paymentMethod.Token).Returns(deleteBraintreePaymentMethodResult);

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.Received(1).DeleteAsync(paymentMethod.Token);
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

        var customer = new Customer
        {
            Id = organization.GatewayCustomerId,
            Sources = new StripeList<IPaymentSource> { Data = sources }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        stripeAdapter
            .CustomerListPaymentMethods(organization.GatewayCustomerId)
            .Returns([]);

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await stripeAdapter.Received(1).BankAccountDeleteAsync(customer.Id, bankAccountId);

        await stripeAdapter.Received(1).CardDeleteAsync(customer.Id, cardId);

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

        var stripeCustomer = new Customer
        {
            Id = organization.GatewayCustomerId,
            Sources = new StripeList<IPaymentSource> { Data = sources }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(stripeCustomer);

        stripeAdapter
            .CustomerListPaymentMethods(organization.GatewayCustomerId)
            .Returns([
                new PaymentMethod { Id = bankAccountId },
                new PaymentMethod { Id = cardId }
            ]);

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await stripeAdapter.DidNotReceiveWithAnyArgs().BankAccountDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.DidNotReceiveWithAnyArgs().CardDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(bankAccountId);

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(cardId);
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
    public async Task UpdatePaymentMethod_NullSubscriber_ArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.UpdatePaymentMethod(null, null));

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_NullTokenizedPaymentMethod_ArgumentNullException(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.UpdatePaymentMethod(provider, null));

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_NoToken_ContactSupport(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.UpdatePaymentMethod(provider, new TokenizedPaymentMethodDTO(PaymentMethodType.Card, null)));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_UnsupportedPaymentMethod_ContactSupport(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.UpdatePaymentMethod(provider, new TokenizedPaymentMethodDTO(PaymentMethodType.BitPay, "TOKEN")));
    }

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_BankAccount_IncorrectNumberOfSetupIntentsForToken_ContactSupport(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.CustomerGetAsync(provider.GatewayCustomerId)
            .Returns(new Customer());

        stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options => options.PaymentMethod == "TOKEN"))
            .Returns([new SetupIntent(), new SetupIntent()]);

        await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.UpdatePaymentMethod(provider, new TokenizedPaymentMethodDTO(PaymentMethodType.BankAccount, "TOKEN")));
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

        await sutProvider.Sut.UpdatePaymentMethod(provider,
            new TokenizedPaymentMethodDTO(PaymentMethodType.BankAccount, "TOKEN"));

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

        await sutProvider.Sut.UpdatePaymentMethod(provider,
            new TokenizedPaymentMethodDTO(PaymentMethodType.Card, "TOKEN"));

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

    // TODO: Braintree remains un-mockable.
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

        var taxInformation = new TaxInformationDTO(
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
    public async Task VerifyBankAccount_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider) => await Assert.ThrowsAsync<ArgumentNullException>(
        () => sutProvider.Sut.VerifyBankAccount(null, (0, 0)));

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_NoSetupIntentId_ContactSupport(
        Provider provider,
        SutProvider<SubscriberService> sutProvider) => await ThrowsContactSupportAsync(() => sutProvider.Sut.VerifyBankAccount(provider, (1, 1)));

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_MakesCorrectInvocations(
        Provider provider,
        SutProvider<SubscriberService> sutProvider)
    {
        var setupIntent = new SetupIntent
        {
            Id = "setup_intent_id",
            PaymentMethodId = "payment_method_id"
        };

        sutProvider.GetDependency<ISetupIntentCache>().Get(provider.Id).Returns(setupIntent.Id);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SetupIntentGet(setupIntent.Id).Returns(setupIntent);

        await sutProvider.Sut.VerifyBankAccount(provider, (1, 1));

        await stripeAdapter.Received(1).SetupIntentVerifyMicroDeposit(setupIntent.Id,
            Arg.Is<SetupIntentVerifyMicrodepositsOptions>(
                options => options.Amounts[0] == 1 && options.Amounts[1] == 1));

        await stripeAdapter.Received(1).PaymentMethodAttachAsync(setupIntent.PaymentMethodId,
            Arg.Is<PaymentMethodAttachOptions>(
                options => options.Customer == provider.GatewayCustomerId));

        await stripeAdapter.Received(1).CustomerUpdateAsync(provider.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options => options.InvoiceSettings.DefaultPaymentMethod == setupIntent.PaymentMethodId));
    }

    #endregion
}
