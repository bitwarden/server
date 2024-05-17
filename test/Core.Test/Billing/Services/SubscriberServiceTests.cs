using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services.Implementations;
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

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(paymentMethod.Token);

        await customerGateway.DidNotReceive().UpdateAsync(braintreeCustomerId, Arg.Is<CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_PaymentMethodDeleteFails_RollBack_ContactSupport(
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

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

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

        await sutProvider.Sut.RemovePaymentMethod(organization);

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

        await sutProvider.Sut.RemovePaymentMethod(organization);

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

    #region GetTaxInformationAsync
    [Theory, BitAutoData]
    public async Task GetTaxInformationAsync_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetTaxInformationAsync(null));

    [Theory, BitAutoData]
    public async Task GetTaxInformationAsync_NoGatewayCustomerId_ReturnsNull(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        subscriber.GatewayCustomerId = null;

        var taxInfo = await sutProvider.Sut.GetTaxInformationAsync(subscriber);

        Assert.Null(taxInfo);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformationAsync_NoCustomer_ReturnsNull(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(subscriber.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns((Customer)null);

        var taxInfo = await sutProvider.Sut.GetTaxInformationAsync(subscriber);

        Assert.Null(taxInfo);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformationAsync_StripeException_ReturnsNull(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(subscriber.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .ThrowsAsync(new StripeException());

        var taxInfo = await sutProvider.Sut.GetTaxInformationAsync(subscriber);

        Assert.Null(taxInfo);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformationAsync_Succeeds(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer
        {
            Address = new Stripe.Address
            {
                Line1 = "123 Main St",
                Line2 = "Apt 4B",
                City = "Metropolis",
                State = "NY",
                PostalCode = "12345",
                Country = "US"
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(subscriber.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var taxInfo = await sutProvider.Sut.GetTaxInformationAsync(subscriber);

        Assert.NotNull(taxInfo);
        Assert.Equal("123 Main St", taxInfo.BillingAddressLine1);
        Assert.Equal("Apt 4B", taxInfo.BillingAddressLine2);
        Assert.Equal("Metropolis", taxInfo.BillingAddressCity);
        Assert.Equal("NY", taxInfo.BillingAddressState);
        Assert.Equal("12345", taxInfo.BillingAddressPostalCode);
        Assert.Equal("US", taxInfo.BillingAddressCountry);
    }
    #endregion

    #region GetPaymentMethodAsync
    [Theory, BitAutoData]
    public async Task GetPaymentMethodAsync_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetPaymentMethodAsync(null));
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethodAsync_NoCustomer_ReturnsNull(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        subscriber.GatewayCustomerId = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(subscriber.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns((Customer)null);

        var billingSource = await sutProvider.Sut.GetPaymentMethodAsync(subscriber);

        Assert.Null(billingSource);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethodAsync_StripeCardPaymentMethod_ReturnsBillingSource(
        Provider subscriber,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer();
        var paymentMethod = CreateSamplePaymentMethod();
        subscriber.GatewayCustomerId = "test_customer_id";
        customer.InvoiceSettings = new CustomerInvoiceSettings
        {
            DefaultPaymentMethod = paymentMethod
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(subscriber.GatewayCustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        var billingSource = await sutProvider.Sut.GetPaymentMethodAsync(subscriber);

        Assert.NotNull(billingSource);
        Assert.Equal(paymentMethod.Card.Brand, billingSource.CardBrand);
    }

    private static PaymentMethod CreateSamplePaymentMethod()
    {
        var paymentMethod = new PaymentMethod
        {
            Id = "pm_test123",
            Type = "card",
            Card = new PaymentMethodCard
            {
                Brand = "visa",
                Last4 = "4242",
                ExpMonth = 12,
                ExpYear = 2024
            }
        };
        return paymentMethod;
    }

    #endregion
}
